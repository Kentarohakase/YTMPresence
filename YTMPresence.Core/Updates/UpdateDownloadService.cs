using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace YTMPresence.Core.Updates;

public sealed record UpdateDownloadProgress(long BytesDownloaded, long? TotalBytes);

public sealed record UpdateDownloadOptions
{
  public UpdateUrlValidator UrlValidator { get; init; } = new();
  public DownloadSizePolicy SizePolicy { get; init; } = DownloadSizePolicy.Default;

  /// <summary>Maximale Redirect-Anzahl pro Download (GitHub benötigt real genau einen).</summary>
  public int MaxRedirects { get; init; } = 5;

  /// <summary>Timeout für Verbindungsaufbau + Response-Header pro Request.</summary>
  public TimeSpan HeaderTimeout { get; init; } = TimeSpan.FromSeconds(30);

  /// <summary>Timeout pro einzelnem Lese-/Schreibschritt (erkennt eingefrorene Verbindungen).</summary>
  public TimeSpan ReadStallTimeout { get; init; } = TimeSpan.FromSeconds(60);

  /// <summary>Obergrenze für den gesamten Setup-Download.</summary>
  public TimeSpan OverallDownloadTimeout { get; init; } = TimeSpan.FromMinutes(30);

  /// <summary>Obergrenze für den gesamten Prüfsummen-Download.</summary>
  public TimeSpan OverallChecksumsTimeout { get; init; } = TimeSpan.FromMinutes(2);
}

/// <summary>
/// Lädt Prüfsummendatei und Setup eines Updates herunter und verifiziert sie.
///
/// Ablauf pro Versuch: eindeutiges Temp-Verzeichnis → SHA256SUMS.txt laden →
/// erwarteten Hash exakt auflösen → Setup in eine .part-Datei streamen
/// (Größenlimit + Timeouts) → Stream schließen → SHA256 berechnen → bei
/// Übereinstimmung atomar auf den finalen Namen verschieben. Jeder Fehler
/// oder Abbruch räumt das Temp-Verzeichnis bestmöglich auf.
///
/// Redirects werden manuell verarbeitet, damit jede Zwischen-URL gegen die
/// Allowlist geprüft werden kann.
/// </summary>
public sealed class UpdateDownloadService : IDisposable
{
  private const int CopyBufferBytes = 128 * 1024;

  private readonly UpdateDownloadOptions _options;
  private readonly HttpClient _http;

  public UpdateDownloadService(UpdateDownloadOptions? options = null, HttpMessageHandler? handler = null)
  {
    _options = options ?? new UpdateDownloadOptions();

    // Redirects deaktiviert: jede Weiterleitung läuft durch den Validator.
    // Im Test kann ein Fake-Handler injiziert werden; sein Lebenszyklus bleibt beim Aufrufer.
    _http = handler is null
      ? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }, disposeHandler: true)
      : new HttpClient(handler, disposeHandler: false);

    _http.Timeout = Timeout.InfiniteTimeSpan; // Timeouts werden selbst verwaltet (Header/Stall/Gesamt).
    _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("YTMPresence", GetProductVersion()));
  }

  /// <summary>
  /// Lädt und verifiziert das Setup aus <paramref name="update"/>. Ohne verfügbare
  /// und passende SHA256-Prüfsumme wird kein Paket zurückgegeben (fail closed).
  /// </summary>
  public async Task<VerifiedSetupPackage> DownloadAndVerifyAsync(
    UpdateCheckResult update,
    IProgress<UpdateDownloadProgress>? progress = null,
    CancellationToken ct = default,
    string? workspaceRoot = null)
  {
    ArgumentNullException.ThrowIfNull(update);

    if (string.IsNullOrWhiteSpace(update.SetupDownloadUrl))
      throw new UpdateSecurityException("Dieses Release enthält kein Setup-Asset.");

    if (string.IsNullOrWhiteSpace(update.ChecksumsDownloadUrl))
      throw new UpdateSecurityException(
        "Dieses Release enthält keine SHA256SUMS.txt. Ohne Prüfsumme wird kein Update ausgeführt.");

    var setupUri = _options.UrlValidator.Validate(update.SetupDownloadUrl, "Setup-Download");
    var checksumsUri = _options.UrlValidator.Validate(update.ChecksumsDownloadUrl, "Prüfsummen-Download");
    var fileName = ResolveSetupFileName(update, setupUri);
    EnsureOriginalAssetFileName(checksumsUri, UpdateArtifactNames.ChecksumsFileName, "Prüfsummen-Download");

    var workspace = TemporaryUpdateWorkspace.Create(workspaceRoot);
    try
    {
      var checksumsText = await DownloadTextAsync(checksumsUri, _options.SizePolicy.MaxChecksumsBytes, ct)
        .ConfigureAwait(false);
      var expectedHash = ChecksumFileParser.GetRequiredSha256(checksumsText, fileName);

      var partPath = Path.Combine(workspace.Directory, fileName + ".part");
      var finalPath = Path.Combine(workspace.Directory, fileName);

      var length = await DownloadSetupToFileAsync(setupUri, partPath, progress, ct).ConfigureAwait(false);

      var actualHash = await ComputeSha256Async(partPath, ct).ConfigureAwait(false);
      if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
        throw new UpdateSecurityException(
          "SHA256-Prüfsumme stimmt nicht überein. Die heruntergeladene Datei wird verworfen.");

      // Atomarer Wechsel im selben Verzeichnis: Erst ab hier existiert eine
      // ausführbare Setup-Datei; eine .part-Datei wird nie gestartet.
      File.Move(partPath, finalPath);

      Logger.Info($"Update setup verified: {fileName} ({length} bytes, SHA256 {expectedHash}).");
      return new VerifiedSetupPackage(
        finalPath, fileName, expectedHash, length, workspace, _options.SizePolicy.MaxSetupBytes);
    }
    catch
    {
      workspace.Dispose();
      throw;
    }
  }

  /// <summary>
  /// Ermittelt den Setup-Dateinamen aus dem Release-Asset (bevorzugt) oder der URL.
  /// Es gibt bewusst keinen synthetischen Fallback-Namen: Ohne exakt bekannten
  /// Namen kann keine Prüfsummenzeile exakt zugeordnet werden.
  /// </summary>
  private static string ResolveSetupFileName(UpdateCheckResult update, Uri setupUri)
  {
    var candidate = !string.IsNullOrWhiteSpace(update.SetupAssetName)
      ? update.SetupAssetName.Trim()
      : Uri.UnescapeDataString(setupUri.Segments[^1]);

    if (string.IsNullOrWhiteSpace(candidate) ||
        candidate.Length > 128 ||
        !string.Equals(Path.GetFileName(candidate), candidate, StringComparison.Ordinal) ||
        candidate.Contains("..", StringComparison.Ordinal) ||
        !candidate.EndsWith(UpdateArtifactNames.SetupFileNameSuffix, StringComparison.OrdinalIgnoreCase) ||
        !candidate.All(static c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '_'))
    {
      throw new UpdateSecurityException("Der Setup-Dateiname des Releases ist ungültig oder nicht erlaubt.");
    }

    EnsureOriginalAssetFileName(setupUri, candidate, "Setup-Download");
    return candidate;
  }

  private static void EnsureOriginalAssetFileName(Uri uri, string expectedFileName, string context)
  {
    var escapedSegment = uri.Segments.Length == 0 ? "" : uri.Segments[^1];
    var actualFileName = Uri.UnescapeDataString(escapedSegment);
    if (!string.Equals(actualFileName, expectedFileName, StringComparison.Ordinal))
      throw new UpdateSecurityException(
        $"{context}: Der URL-Dateiname '{actualFileName}' entspricht nicht exakt dem erwarteten Artefakt '{expectedFileName}'.");
  }

  private async Task<string> DownloadTextAsync(Uri uri, long maxBytes, CancellationToken ct)
  {
    using var overallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    overallCts.CancelAfter(_options.OverallChecksumsTimeout);

    try
    {
      using var response = await SendWithRedirectsAsync(uri, overallCts.Token).ConfigureAwait(false);
      EnsureContentLengthWithinLimit(response, maxBytes, uri);

      await using var buffer = new MemoryStream();
      await CopyBoundedAsync(
        response, buffer, maxBytes, progress: null, totalForProgress: null, overallCts.Token).ConfigureAwait(false);

      return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
        .GetString(buffer.ToArray());
    }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
    {
      throw new UpdateDownloadException("Zeitüberschreitung beim Laden der Prüfsummendatei.");
    }
    catch (HttpRequestException ex)
    {
      throw new UpdateDownloadException($"Netzwerkfehler beim Laden der Prüfsummendatei: {ex.Message}", ex);
    }
    catch (IOException ex)
    {
      throw new UpdateDownloadException($"Der Prüfsummen-Download wurde unterbrochen: {ex.Message}", ex);
    }
    catch (DecoderFallbackException ex)
    {
      throw new UpdateSecurityException("Die Prüfsummendatei ist nicht gültig UTF-8-codiert.", ex);
    }
  }

  private async Task<long> DownloadSetupToFileAsync(
    Uri uri,
    string partPath,
    IProgress<UpdateDownloadProgress>? progress,
    CancellationToken ct)
  {
    using var overallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    overallCts.CancelAfter(_options.OverallDownloadTimeout);

    try
    {
      using var response = await SendWithRedirectsAsync(uri, overallCts.Token).ConfigureAwait(false);
      EnsureContentLengthWithinLimit(response, _options.SizePolicy.MaxSetupBytes, uri);

      var totalBytes = response.Content.Headers.ContentLength;

      long written;
      // CreateNew: Kollision mit einer fremden/alten Datei bricht ab, statt sie zu überschreiben.
      await using (var destination = new FileStream(partPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
      {
        written = await CopyBoundedAsync(
          response, destination, _options.SizePolicy.MaxSetupBytes, progress, totalBytes, overallCts.Token)
          .ConfigureAwait(false);

        await destination.FlushAsync(overallCts.Token).ConfigureAwait(false);
      }

      if (totalBytes.HasValue && written != totalBytes.Value)
        throw new UpdateDownloadException(
          $"Der Download ist unvollständig ({written} von {totalBytes.Value} Bytes). Die Datei wird verworfen.");

      return written;
    }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
    {
      throw new UpdateDownloadException("Zeitüberschreitung beim Herunterladen des Updates.");
    }
    catch (HttpRequestException ex)
    {
      throw new UpdateDownloadException($"Netzwerkfehler beim Herunterladen des Updates: {ex.Message}", ex);
    }
    catch (IOException ex)
    {
      throw new UpdateDownloadException($"Der Update-Download wurde unterbrochen: {ex.Message}", ex);
    }
  }

  /// <summary>
  /// Führt einen GET-Request mit kontrollierter manueller Redirect-Verarbeitung aus.
  /// Jede URL der Kette (inklusive der finalen) läuft durch den Validator.
  /// </summary>
  private async Task<HttpResponseMessage> SendWithRedirectsAsync(Uri uri, CancellationToken ct)
  {
    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var current = uri;

    for (var hop = 0; ; hop++)
    {
      if (!visited.Add(current.AbsoluteUri))
        throw new UpdateSecurityException($"Redirect-Schleife erkannt bei '{current}'. Update wird abgebrochen.");

      using var request = new HttpRequestMessage(HttpMethod.Get, current);

      HttpResponseMessage response;
      using (var headerCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
      {
        headerCts.CancelAfter(_options.HeaderTimeout);
        try
        {
          response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headerCts.Token)
            .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
          throw new UpdateDownloadException($"Zeitüberschreitung beim Verbindungsaufbau zu '{current.Host}'.");
        }
        catch (HttpRequestException ex)
        {
          throw new UpdateDownloadException($"Netzwerkfehler bei '{current.Host}': {ex.Message}", ex);
        }
      }

      if (!IsRedirect(response.StatusCode))
      {
        if (!response.IsSuccessStatusCode)
        {
          var status = (int)response.StatusCode;
          response.Dispose();
          throw new UpdateDownloadException($"Download fehlgeschlagen: HTTP {status} von '{current.Host}'.");
        }

        return response;
      }

      Uri next;
      try
      {
        if (hop >= _options.MaxRedirects)
          throw new UpdateSecurityException("Zu viele Redirects. Update wird abgebrochen.");

        var location = response.Headers.Location
          ?? throw new UpdateSecurityException("Redirect ohne Location-Header. Update wird abgebrochen.");

        // Relative Redirects werden gegen die aktuelle URL aufgelöst und
        // anschließend wie jede andere URL vollständig validiert.
        var resolved = location.IsAbsoluteUri ? location : new Uri(current, location);
        next = _options.UrlValidator.Validate(resolved, "Redirect");
      }
      finally
      {
        response.Dispose();
      }

      current = next;
    }
  }

  private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
    HttpStatusCode.MovedPermanently or
    HttpStatusCode.Found or
    HttpStatusCode.SeeOther or
    HttpStatusCode.TemporaryRedirect or
    HttpStatusCode.PermanentRedirect;

  private static void EnsureContentLengthWithinLimit(HttpResponseMessage response, long maxBytes, Uri uri)
  {
    var contentLength = response.Content.Headers.ContentLength;
    if (contentLength.HasValue && contentLength.Value > maxBytes)
      throw new UpdateDownloadException(
        $"Der Download von '{uri.Host}' ist mit {contentLength.Value} Bytes größer als das Limit von {maxBytes} Bytes.");
  }

  /// <summary>
  /// Kopiert den Response-Body mit hartem Größenlimit und Stall-Timeout pro
  /// Leseschritt. Kein ReadAsByteArrayAsync/ReadToEndAsync: Das Limit gilt
  /// unabhängig vom Content-Length-Header.
  /// </summary>
  private async Task<long> CopyBoundedAsync(
    HttpResponseMessage response,
    Stream destination,
    long maxBytes,
    IProgress<UpdateDownloadProgress>? progress,
    long? totalForProgress,
    CancellationToken ct)
  {
    await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

    var buffer = new byte[CopyBufferBytes];
    long written = 0;

    while (true)
    {
      int read;
      using (var stallCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
      {
        stallCts.CancelAfter(_options.ReadStallTimeout);
        try
        {
          read = await source.ReadAsync(buffer, stallCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
          throw new UpdateDownloadException("Die Verbindung liefert keine Daten mehr (Stall-Timeout).");
        }
      }

      if (read == 0)
        break;

      written += read;
      if (written > maxBytes)
        throw new UpdateDownloadException(
          $"Der Download überschreitet das Größenlimit von {maxBytes} Bytes und wurde abgebrochen.");

      await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
      progress?.Report(new UpdateDownloadProgress(written, totalForProgress));
    }

    return written;
  }

  private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
  {
    await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
    return Convert.ToHexString(hash).ToLowerInvariant();
  }

  private static string GetProductVersion()
  {
    var version = Assembly.GetExecutingAssembly()
      .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
      .InformationalVersion;

    return string.IsNullOrWhiteSpace(version) ? "dev" : version;
  }

  public void Dispose() => _http.Dispose();
}
