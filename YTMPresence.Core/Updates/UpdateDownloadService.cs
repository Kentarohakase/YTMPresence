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
      throw new UpdateSecurityException("This release has no setup asset.");

    if (string.IsNullOrWhiteSpace(update.ChecksumsDownloadUrl))
      throw new UpdateSecurityException(
        "This release has no SHA256SUMS.txt. Without a checksum no update is run.");

    var setupUri = _options.UrlValidator.Validate(update.SetupDownloadUrl, "Setup download");
    var checksumsUri = _options.UrlValidator.Validate(update.ChecksumsDownloadUrl, "Checksums download");
    var fileName = ResolveSetupFileName(update, setupUri);
    EnsureOriginalAssetFileName(checksumsUri, UpdateArtifactNames.ChecksumsFileName, "Checksums download");

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
          "The SHA256 checksum does not match. The downloaded file is discarded.");

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
      throw new UpdateSecurityException("The release's setup file name is invalid or not allowed.");
    }

    EnsureOriginalAssetFileName(setupUri, candidate, "Setup download");
    return candidate;
  }

  private static void EnsureOriginalAssetFileName(Uri uri, string expectedFileName, string context)
  {
    var escapedSegment = uri.Segments.Length == 0 ? "" : uri.Segments[^1];
    var actualFileName = Uri.UnescapeDataString(escapedSegment);
    if (!string.Equals(actualFileName, expectedFileName, StringComparison.Ordinal))
      throw new UpdateSecurityException(
        $"{context}: the file name '{actualFileName}' in the URL does not exactly match the expected artifact '{expectedFileName}'.");
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
      throw new UpdateDownloadException("Timed out while loading the checksums file.");
    }
    catch (HttpRequestException ex)
    {
      throw new UpdateDownloadException($"Network error while loading the checksums file: {ex.Message}", ex);
    }
    catch (IOException ex)
    {
      throw new UpdateDownloadException($"The checksums download was interrupted: {ex.Message}", ex);
    }
    catch (DecoderFallbackException ex)
    {
      throw new UpdateSecurityException("The checksums file is not valid UTF-8.", ex);
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
          $"The download is incomplete ({written} of {totalBytes.Value} bytes). The file is discarded.");

      return written;
    }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
    {
      throw new UpdateDownloadException("Timed out while downloading the update.");
    }
    catch (HttpRequestException ex)
    {
      throw new UpdateDownloadException($"Network error while downloading the update: {ex.Message}", ex);
    }
    catch (IOException ex)
    {
      throw new UpdateDownloadException($"The update download was interrupted: {ex.Message}", ex);
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
        throw new UpdateSecurityException($"Redirect loop detected at '{current}'. The update is cancelled.");

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
          throw new UpdateDownloadException($"Timed out while connecting to '{current.Host}'.");
        }
        catch (HttpRequestException ex)
        {
          throw new UpdateDownloadException($"Network error at '{current.Host}': {ex.Message}", ex);
        }
      }

      if (!IsRedirect(response.StatusCode))
      {
        if (!response.IsSuccessStatusCode)
        {
          var status = (int)response.StatusCode;
          response.Dispose();
          throw new UpdateDownloadException($"Download failed: HTTP {status} from '{current.Host}'.");
        }

        return response;
      }

      Uri next;
      try
      {
        if (hop >= _options.MaxRedirects)
          throw new UpdateSecurityException("Too many redirects. The update is cancelled.");

        var location = response.Headers.Location
          ?? throw new UpdateSecurityException("Redirect without a Location header. The update is cancelled.");

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
        $"The download from '{uri.Host}' has {contentLength.Value} bytes, more than the limit of {maxBytes} bytes.");
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
          throw new UpdateDownloadException("The connection stopped delivering data (stall timeout).");
        }
      }

      if (read == 0)
        break;

      written += read;
      if (written > maxBytes)
        throw new UpdateDownloadException(
          $"The download exceeds the size limit of {maxBytes} bytes and was cancelled.");

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
