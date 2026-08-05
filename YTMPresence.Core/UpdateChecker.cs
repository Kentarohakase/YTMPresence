using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using YTMPresence.Core.Updates;

namespace YTMPresence.Core;

public sealed record UpdateCheckResult(
  bool IsUpdateAvailable,
  string CurrentVersion,
  string LatestVersion,
  string ReleaseUrl,
  string? ErrorMessage = null,
  string? SetupDownloadUrl = null,
  string? SetupAssetName = null,
  string? ChecksumsDownloadUrl = null);

public static class UpdateChecker
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true
  };

  public static async Task<UpdateCheckResult> CheckLatestAsync(
    string currentVersion,
    AppSettings settings,
    CancellationToken ct,
    HttpMessageHandler? handler = null)
  {
    if (string.IsNullOrWhiteSpace(settings.UpdateApiUrl))
      return Error(currentVersion, "Update-URL ist leer.");

    var urlValidator = UpdateUrlValidator.ForSettings(settings);
    if (!urlValidator.TryValidate(settings.UpdateApiUrl, "Update-Check", out var apiUri))
      return Error(currentVersion, "Update-URL ist nicht als Updatequelle erlaubt (nur HTTPS zu GitHub).");

    try
    {
      // Im Test kann ein Fake-Handler injiziert werden; der Handler-Lebenszyklus
      // bleibt dann beim Aufrufer (disposeHandler: false).
      using var http = handler is null
        ? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }, disposeHandler: true)
        : new HttpClient(handler, disposeHandler: false);
      http.Timeout = Timeout.InfiniteTimeSpan;
      http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("YTMPresence", GetProductVersion()));
      http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

      using var overallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      overallCts.CancelAfter(TimeSpan.FromMinutes(2));

      using var response = await SendWithValidatedRedirectsAsync(
        http, apiUri!, urlValidator, overallCts.Token);
      if (!response.IsSuccessStatusCode)
        return Error(currentVersion, $"Update-Check fehlgeschlagen: HTTP {(int)response.StatusCode}.");

      var maxMetadataBytes = DownloadSizePolicy.Default.MaxMetadataBytes;
      if (response.Content.Headers.ContentLength is > 0 && response.Content.Headers.ContentLength > maxMetadataBytes)
        return Error(currentVersion, "Update-Check fehlgeschlagen: Release-Antwort ist zu groß.");

      // Begrenztes Einlesen: Das Limit gilt auch ohne Content-Length-Header.
      await using var stream = await response.Content.ReadAsStreamAsync(ct);
      var body = await ReadBoundedAsync(stream, maxMetadataBytes, overallCts.Token);
      if (body is null)
        return Error(currentVersion, "Update-Check fehlgeschlagen: Release-Antwort ist zu groß.");

      var release = JsonSerializer.Deserialize<GitHubRelease>(body, JsonOptions);

      if (release is null || release.Draft || string.IsNullOrWhiteSpace(release.TagName))
        return Error(currentVersion, "Keine gültige Release-Antwort erhalten.");

      var latestVersion = NormalizeVersionText(release.TagName);
      var releaseUrl = release.HtmlUrl is { Length: > 0 } htmlUrl && urlValidator.TryValidate(htmlUrl, "Release-Seite", out _)
        ? htmlUrl
        : "https://github.com/Kentarohakase/YTMPresence/releases";
      var setupDownloadUrl = release.Assets?
        .Where(a => a.Name?.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase) == true)
        .Select(a => a.BrowserDownloadUrl)
        .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));
      var setupAssetName = release.Assets?
        .Where(a => a.Name?.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase) == true)
        .Select(a => a.Name)
        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
      var checksumsDownloadUrl = release.Assets?
        .Where(a => string.Equals(a.Name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
        .Select(a => a.BrowserDownloadUrl)
        .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));

      // Asset-URLs, die die Update-Richtlinie verletzen, machen den ganzen
      // Check sichtbar ungültig, statt still einen Download anzubieten.
      if (setupDownloadUrl is not null && !urlValidator.TryValidate(setupDownloadUrl, "Setup-Asset", out _))
        return Error(currentVersion, "Update-Check fehlgeschlagen: Die Setup-Download-URL ist nicht als Updatequelle erlaubt.");

      if (checksumsDownloadUrl is not null && !urlValidator.TryValidate(checksumsDownloadUrl, "Prüfsummen-Asset", out _))
        return Error(currentVersion, "Update-Check fehlgeschlagen: Die Prüfsummen-URL ist nicht als Updatequelle erlaubt.");

      var isNewer = IsNewer(latestVersion, currentVersion);
      return new UpdateCheckResult(
        isNewer,
        currentVersion,
        latestVersion,
        releaseUrl,
        SetupDownloadUrl: setupDownloadUrl,
        SetupAssetName: setupAssetName,
        ChecksumsDownloadUrl: checksumsDownloadUrl);
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
      throw;
    }
    catch (OperationCanceledException)
    {
      return Error(currentVersion, "Update-Check fehlgeschlagen: Zeitüberschreitung beim Laden der Release-Metadaten.");
    }
    catch (Exception ex)
    {
      return Error(currentVersion, $"Update-Check fehlgeschlagen: {ex.Message}");
    }
  }

  /// <summary>Liest höchstens <paramref name="maxBytes"/>; null bei Überschreitung.</summary>
  private static async Task<byte[]?> ReadBoundedAsync(Stream stream, long maxBytes, CancellationToken ct)
  {
    using var buffer = new MemoryStream();
    var chunk = new byte[64 * 1024];

    while (true)
    {
      using var stallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      stallCts.CancelAfter(TimeSpan.FromSeconds(30));
      var read = await stream.ReadAsync(chunk, stallCts.Token);
      if (read == 0)
        return buffer.ToArray();

      if (buffer.Length + read > maxBytes)
        return null;

      buffer.Write(chunk, 0, read);
    }
  }

  private static async Task<HttpResponseMessage> SendWithValidatedRedirectsAsync(
    HttpClient http,
    Uri initialUri,
    UpdateUrlValidator validator,
    CancellationToken ct)
  {
    const int maxRedirects = 5;
    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var current = initialUri;

    for (var hop = 0; ; hop++)
    {
      current = validator.Validate(current, hop == 0 ? "Update-Check" : "Update-Check-Redirect");
      if (!visited.Add(current.AbsoluteUri))
        throw new UpdateSecurityException("Update-Check fehlgeschlagen: Redirect-Schleife erkannt.");

      using var request = new HttpRequestMessage(HttpMethod.Get, current);
      HttpResponseMessage response;

      using (var headerCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
      {
        headerCts.CancelAfter(TimeSpan.FromSeconds(30));
        response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headerCts.Token);
      }

      if (!IsRedirect(response.StatusCode))
        return response;

      try
      {
        if (hop >= maxRedirects)
          throw new UpdateSecurityException("Update-Check fehlgeschlagen: Zu viele Redirects.");

        var location = response.Headers.Location
          ?? throw new UpdateSecurityException("Update-Check fehlgeschlagen: Redirect ohne Location-Header.");

        current = location.IsAbsoluteUri ? location : new Uri(current, location);
      }
      finally
      {
        response.Dispose();
      }
    }
  }

  private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
    HttpStatusCode.MovedPermanently or
    HttpStatusCode.Found or
    HttpStatusCode.SeeOther or
    HttpStatusCode.TemporaryRedirect or
    HttpStatusCode.PermanentRedirect;

  private static UpdateCheckResult Error(string currentVersion, string message)
    => new(false, currentVersion, currentVersion, "https://github.com/Kentarohakase/YTMPresence/releases", message);

  private static bool IsNewer(string latestVersion, string currentVersion)
  {
    if (!TryParseVersion(latestVersion, out var latest))
      return false;

    if (!TryParseVersion(currentVersion, out var current))
      return false;

    return latest.CompareTo(current) > 0;
  }

  private static bool TryParseVersion(string value, out Version version)
  {
    version = new Version(0, 0, 0);
    var normalized = NormalizeVersionText(value);
    var main = normalized.Split('+', 2)[0].Split('-', 2)[0];
    var parts = main.Split('.', StringSplitOptions.RemoveEmptyEntries);

    if (parts.Length is < 2 or > 4)
      return false;

    var numbers = new int[4] { 0, 0, 0, 0 };
    for (var i = 0; i < parts.Length; i++)
    {
      if (!int.TryParse(parts[i], out numbers[i]))
        return false;
    }

    version = parts.Length switch
    {
      2 => new Version(numbers[0], numbers[1]),
      3 => new Version(numbers[0], numbers[1], numbers[2]),
      _ => new Version(numbers[0], numbers[1], numbers[2], numbers[3])
    };

    return true;
  }

  private static string NormalizeVersionText(string value)
  {
    var trimmed = (value ?? "").Trim();
    return trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase)
      ? trimmed[1..]
      : trimmed;
  }

  private static string GetProductVersion()
  {
    var version = Assembly.GetExecutingAssembly()
      .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
      .InformationalVersion;

    return string.IsNullOrWhiteSpace(version) ? "dev" : version;
  }

  private sealed class GitHubRelease
  {
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubReleaseAsset>? Assets { get; set; }
  }

  private sealed class GitHubReleaseAsset
  {
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }
  }
}
