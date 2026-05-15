using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YTMPresence.Core;

public sealed record UpdateCheckResult(
  bool IsUpdateAvailable,
  string CurrentVersion,
  string LatestVersion,
  string ReleaseUrl,
  string? ErrorMessage = null,
  string? SetupDownloadUrl = null);

public static class UpdateChecker
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true
  };

  public static async Task<UpdateCheckResult> CheckLatestAsync(
    string currentVersion,
    AppSettings settings,
    CancellationToken ct)
  {
    if (string.IsNullOrWhiteSpace(settings.UpdateApiUrl))
      return Error(currentVersion, "Update-URL ist leer.");

    try
    {
      using var http = new HttpClient();
      http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("YTMPresence", GetProductVersion()));
      http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

      using var response = await http.GetAsync(settings.UpdateApiUrl, ct);
      if (!response.IsSuccessStatusCode)
        return Error(currentVersion, $"Update-Check fehlgeschlagen: HTTP {(int)response.StatusCode}.");

      await using var stream = await response.Content.ReadAsStreamAsync(ct);
      var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, ct);

      if (release is null || release.Draft || string.IsNullOrWhiteSpace(release.TagName))
        return Error(currentVersion, "Keine gültige Release-Antwort erhalten.");

      var latestVersion = NormalizeVersionText(release.TagName);
      var releaseUrl = string.IsNullOrWhiteSpace(release.HtmlUrl)
        ? "https://github.com/Kentarohakase/YTMPresence/releases"
        : release.HtmlUrl;
      var setupDownloadUrl = release.Assets?
        .Where(a => a.Name?.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase) == true)
        .Select(a => a.BrowserDownloadUrl)
        .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));

      var isNewer = IsNewer(latestVersion, currentVersion);
      return new UpdateCheckResult(
        isNewer,
        currentVersion,
        latestVersion,
        releaseUrl,
        SetupDownloadUrl: setupDownloadUrl);
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception ex)
    {
      return Error(currentVersion, $"Update-Check fehlgeschlagen: {ex.Message}");
    }
  }

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
