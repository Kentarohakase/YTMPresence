using System.Net;
using YTMPresence.Core;

namespace YTMPresence.Core.Tests;

public class UpdateCheckerTests
{
  private const string SetupUrl = "https://github.com/Kentarohakase/YTMPresence/releases/download/v1.0.0/YTMPresence-1.0.0-win-x64-setup.exe";
  private const string ChecksumsUrl = "https://github.com/Kentarohakase/YTMPresence/releases/download/v1.0.0/SHA256SUMS.txt";

  private sealed class StubHandler(HttpStatusCode code, string body) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
      => Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
  }

  private static AppSettings Settings() => new()
  {
    UpdateApiUrl = "https://api.github.com/repos/Kentarohakase/YTMPresence/releases/latest"
  };

  private static string ReleaseJson(
    string tag,
    bool draft = false,
    bool withAssets = true,
    string setupUrl = SetupUrl,
    string checksumsUrl = ChecksumsUrl)
  {
    var assets = withAssets
      ? $$"""
        "assets": [
          { "name": "YTMPresence-1.0.0-win-x64-setup.exe", "browser_download_url": "{{setupUrl}}" },
          { "name": "SHA256SUMS.txt", "browser_download_url": "{{checksumsUrl}}" }
        ],
        """
      : "";

    return $$"""
      {
        "tag_name": "{{tag}}",
        "html_url": "https://github.com/Kentarohakase/YTMPresence/releases/tag/{{tag}}",
        "draft": {{(draft ? "true" : "false")}},
        {{assets}}
        "name": "release"
      }
      """;
  }

  private static Task<UpdateCheckResult> Check(string current, string body, HttpStatusCode code = HttpStatusCode.OK)
    => UpdateChecker.CheckLatestAsync(current, Settings(), CancellationToken.None, new StubHandler(code, body));

  [Fact]
  public async Task NewerVersion_IsUpdateAvailable()
  {
    var result = await Check("0.8.11", ReleaseJson("v1.0.0"));

    Assert.True(result.IsUpdateAvailable);
    Assert.Equal("1.0.0", result.LatestVersion);
    Assert.Equal(SetupUrl, result.SetupDownloadUrl);
    Assert.Equal("YTMPresence-1.0.0-win-x64-setup.exe", result.SetupAssetName);
    Assert.Equal(ChecksumsUrl, result.ChecksumsDownloadUrl);
    Assert.Null(result.ErrorMessage);
  }

  [Fact]
  public async Task SameVersion_NoUpdate()
  {
    var result = await Check("0.8.11", ReleaseJson("v0.8.11"));
    Assert.False(result.IsUpdateAvailable);
  }

  [Fact]
  public async Task OlderVersion_NoUpdate()
  {
    var result = await Check("1.0.0", ReleaseJson("v0.8.0"));
    Assert.False(result.IsUpdateAvailable);
  }

  [Fact]
  public async Task VPrefix_IsNormalized()
  {
    var result = await Check("0.0.1", ReleaseJson("v1.2.3"));
    Assert.Equal("1.2.3", result.LatestVersion);
  }

  [Fact]
  public async Task Draft_ReturnsError()
  {
    var result = await Check("0.8.11", ReleaseJson("v1.0.0", draft: true));
    Assert.False(result.IsUpdateAvailable);
    Assert.NotNull(result.ErrorMessage);
  }

  [Fact]
  public async Task HttpError_ReturnsError()
  {
    var result = await Check("0.8.11", "{}", HttpStatusCode.InternalServerError);
    Assert.False(result.IsUpdateAvailable);
    Assert.Contains("500", result.ErrorMessage);
  }

  [Fact]
  public async Task EmptyUpdateUrl_ReturnsError()
  {
    var settings = new AppSettings { UpdateApiUrl = "" };
    var result = await UpdateChecker.CheckLatestAsync("0.8.11", settings, CancellationToken.None);
    Assert.False(result.IsUpdateAvailable);
    Assert.NotNull(result.ErrorMessage);
  }

  // --- Härtung: URL-Richtlinie und Größenlimit -----------------------------

  [Fact]
  public async Task HttpUpdateApiUrlInProduction_ReturnsError()
  {
    var settings = new AppSettings { UpdateApiUrl = "http://api.github.com/repos/x/releases/latest" };
    var result = await UpdateChecker.CheckLatestAsync(
      "0.8.11", settings, CancellationToken.None, new StubHandler(HttpStatusCode.OK, ReleaseJson("v1.0.0")));

    Assert.False(result.IsUpdateAvailable);
    Assert.Contains("nicht als Updatequelle erlaubt", result.ErrorMessage);
  }

  [Fact]
  public async Task NonGitHubUpdateApiUrl_ReturnsError()
  {
    var settings = new AppSettings { UpdateApiUrl = "https://example.test/releases/latest" };
    var result = await UpdateChecker.CheckLatestAsync(
      "0.8.11", settings, CancellationToken.None, new StubHandler(HttpStatusCode.OK, ReleaseJson("v1.0.0")));

    Assert.False(result.IsUpdateAvailable);
    Assert.Contains("nicht als Updatequelle erlaubt", result.ErrorMessage);
  }

  [Fact]
  public async Task LoopbackHttpUpdateApiUrl_WorksOnlyInExplicitTestMode()
  {
    var loopbackSettings = new AppSettings
    {
      UpdateApiUrl = "http://127.0.0.1:18111/releases/latest",
      AllowLoopbackUpdateSource = true
    };

    var body = ReleaseJson(
      "v1.0.0",
      setupUrl: "http://127.0.0.1:18111/assets/YTMPresence-1.0.0-win-x64-setup.exe",
      checksumsUrl: "http://127.0.0.1:18111/assets/SHA256SUMS.txt");

    var allowed = await UpdateChecker.CheckLatestAsync(
      "0.8.11", loopbackSettings, CancellationToken.None, new StubHandler(HttpStatusCode.OK, body));
    Assert.True(allowed.IsUpdateAvailable);
    Assert.Null(allowed.ErrorMessage);

    loopbackSettings.AllowLoopbackUpdateSource = false;
    var blocked = await UpdateChecker.CheckLatestAsync(
      "0.8.11", loopbackSettings, CancellationToken.None, new StubHandler(HttpStatusCode.OK, body));
    Assert.False(blocked.IsUpdateAvailable);
    Assert.NotNull(blocked.ErrorMessage);
  }

  [Fact]
  public async Task DisallowedSetupAssetUrl_ReturnsError()
  {
    var result = await Check("0.8.11", ReleaseJson("v1.0.0", setupUrl: "https://evil.example/setup.exe"));
    Assert.False(result.IsUpdateAvailable);
    Assert.Contains("Setup-Download-URL", result.ErrorMessage);
  }

  [Fact]
  public async Task DisallowedChecksumsAssetUrl_ReturnsError()
  {
    var result = await Check("0.8.11", ReleaseJson("v1.0.0", checksumsUrl: "http://github.com/SHA256SUMS.txt"));
    Assert.False(result.IsUpdateAvailable);
    Assert.Contains("Prüfsummen-URL", result.ErrorMessage);
  }

  [Fact]
  public async Task OversizedMetadataResponse_ReturnsError()
  {
    var huge = "{\"tag_name\":\"v1.0.0\",\"padding\":\"" + new string('x', 5 * 1024 * 1024) + "\"}";
    var result = await Check("0.8.11", huge);

    Assert.False(result.IsUpdateAvailable);
    Assert.Contains("zu groß", result.ErrorMessage);
  }

  [Fact]
  public async Task AllowedMetadataRedirect_IsFollowed()
  {
    const string redirectedUrl = "https://api.github.com/repos/Kentarohakase/YTMPresence/releases/latest?redirected=1";
    using var routes = new RouteHandler
    {
      [Settings().UpdateApiUrl] = _ => Responses.Redirect(redirectedUrl),
      [redirectedUrl] = _ => Responses.Text(ReleaseJson("v1.0.0"))
    };

    var result = await UpdateChecker.CheckLatestAsync(
      "0.8.11", Settings(), CancellationToken.None, routes);

    Assert.True(result.IsUpdateAvailable);
    Assert.Equal(2, routes.RequestCount);
  }

  [Theory]
  [InlineData("http://github.com/unsafe")]
  [InlineData("http://127.0.0.1:18111/private")]
  [InlineData("https://raw.githubusercontent.com/owner/repo/main/release.json")]
  [InlineData("https://10.0.0.1/internal")]
  public async Task ForbiddenMetadataRedirect_IsBlocked(string target)
  {
    using var routes = new RouteHandler
    {
      [Settings().UpdateApiUrl] = _ => Responses.Redirect(target)
    };

    var result = await UpdateChecker.CheckLatestAsync(
      "0.8.11", Settings(), CancellationToken.None, routes);

    Assert.False(result.IsUpdateAvailable);
    Assert.NotNull(result.ErrorMessage);
    Assert.Equal(1, routes.RequestCount);
  }

  [Fact]
  public async Task MetadataRedirectLoop_IsBlocked()
  {
    const string redirectedUrl = "https://api.github.com/repos/Kentarohakase/YTMPresence/releases/latest?loop=1";
    using var routes = new RouteHandler
    {
      [Settings().UpdateApiUrl] = _ => Responses.Redirect(redirectedUrl),
      [redirectedUrl] = _ => Responses.Redirect(Settings().UpdateApiUrl)
    };

    var result = await UpdateChecker.CheckLatestAsync(
      "0.8.11", Settings(), CancellationToken.None, routes);

    Assert.False(result.IsUpdateAvailable);
    Assert.Contains("Schleife", result.ErrorMessage);
  }
}
