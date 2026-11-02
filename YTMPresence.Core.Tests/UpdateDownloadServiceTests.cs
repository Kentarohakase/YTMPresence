using System.Net;
using System.Security.Cryptography;

using YTMPresence.Core.Updates;

namespace YTMPresence.Core.Tests;

public class UpdateDownloadServiceTests : IDisposable
{
  private const string SetupName = "YTMPresence-9.9.9-win-x64-setup.exe";
  private const string SetupUrl = "https://github.com/Kentarohakase/YTMPresence/releases/download/v9.9.9/" + SetupName;
  private const string ChecksumsUrl = "https://github.com/Kentarohakase/YTMPresence/releases/download/v9.9.9/SHA256SUMS.txt";
  private const string CdnUrl = "https://release-assets.githubusercontent.com/github-production-release-asset/1";

  private readonly string _testRoot = Path.Combine(
    Path.GetTempPath(), "YTMPresence.Tests", Path.GetRandomFileName());

  private static readonly byte[] SetupBytes = CreateSetupBytes(256 * 1024);
  private static readonly string SetupHash = Convert.ToHexString(SHA256.HashData(SetupBytes)).ToLowerInvariant();

  public void Dispose()
  {
    try
    {
      if (Directory.Exists(_testRoot))
        Directory.Delete(_testRoot, recursive: true);
    }
    catch
    {
      // Best effort: Aufräumfehler dürfen keine Testergebnisse verfälschen.
    }
  }

  private static byte[] CreateSetupBytes(int size)
  {
    var bytes = new byte[size];
    new Random(1234).NextBytes(bytes);
    return bytes;
  }

  private static UpdateCheckResult Update(
    string? setupUrl = SetupUrl,
    string? checksumsUrl = ChecksumsUrl,
    string? assetName = SetupName)
    => new(
      IsUpdateAvailable: true,
      CurrentVersion: "1.0.0",
      LatestVersion: "9.9.9",
      ReleaseUrl: "https://github.com/Kentarohakase/YTMPresence/releases",
      SetupDownloadUrl: setupUrl,
      SetupAssetName: assetName,
      ChecksumsDownloadUrl: checksumsUrl);

  private static string DefaultChecksums(string hash = "", string name = SetupName)
    => $"{(hash.Length == 0 ? SetupHash : hash)}  {name}\n";

  private UpdateDownloadService Service(
    RouteHandler routes,
    long? maxSetupBytes = null,
    bool allowLoopback = false,
    int? maxRedirects = null,
    TimeSpan? overallChecksumsTimeout = null)
  {
    var options = new UpdateDownloadOptions
    {
      UrlValidator = new UpdateUrlValidator(allowLoopback),
      SizePolicy = new DownloadSizePolicy
      {
        MaxSetupBytes = maxSetupBytes ?? DownloadSizePolicy.Default.MaxSetupBytes
      },
      MaxRedirects = maxRedirects ?? 5,
      OverallChecksumsTimeout = overallChecksumsTimeout ?? TimeSpan.FromMinutes(2)
    };

    return new UpdateDownloadService(options, routes);
  }

  private Task<VerifiedSetupPackage> DownloadAsync(
    UpdateDownloadService service,
    UpdateCheckResult? update = null,
    IProgress<UpdateDownloadProgress>? progress = null,
    CancellationToken ct = default)
    => service.DownloadAndVerifyAsync(update ?? Update(), progress, ct, _testRoot);

  // --- Erfolgsfälle ---------------------------------------------------------

  [Fact]
  public async Task ValidDownload_ReturnsVerifiedPackage()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums()),
      [SetupUrl] = _ => Responses.Bytes(SetupBytes)
    };

    var progressReports = new List<UpdateDownloadProgress>();
    var progress = new SynchronousProgress<UpdateDownloadProgress>(progressReports.Add);

    using var service = Service(routes);
    using var package = await DownloadAsync(service, progress: progress);

    Assert.Equal(SetupName, package.FileName);
    Assert.Equal(SetupHash, package.Sha256);
    Assert.Equal(SetupBytes.LongLength, package.Length);
    Assert.True(File.Exists(package.FilePath));
    Assert.Equal(SetupBytes, await File.ReadAllBytesAsync(package.FilePath));
    Assert.False(File.Exists(package.FilePath + ".part"));
    Assert.NotEmpty(progressReports);
    Assert.Equal(SetupBytes.LongLength, progressReports[^1].BytesDownloaded);
  }

  [Fact]
  public async Task PackageDispose_RemovesWorkspace()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums()),
      [SetupUrl] = _ => Responses.Bytes(SetupBytes)
    };

    using var service = Service(routes);
    var package = await DownloadAsync(service);
    var workspace = package.WorkspaceDirectory;

    Assert.True(Directory.Exists(workspace));
    package.Dispose();
    Assert.False(Directory.Exists(workspace));
  }

  [Fact]
  public async Task ParallelAttempts_UseSeparateWorkspaces()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums()),
      [SetupUrl] = _ => Responses.Bytes(SetupBytes)
    };

    using var service = Service(routes);
    var first = DownloadAsync(service);
    var second = DownloadAsync(service);

    using var packageA = await first;
    using var packageB = await second;

    Assert.NotEqual(packageA.WorkspaceDirectory, packageB.WorkspaceDirectory);
    Assert.True(File.Exists(packageA.FilePath));
    Assert.True(File.Exists(packageB.FilePath));
  }

  [Fact]
  public async Task ExistingOldTempFiles_DoNotBlockNewAttempt_AndStaleCleanupRemovesThem()
  {
    var staleDir = Path.Combine(_testRoot, "stale-attempt");
    Directory.CreateDirectory(staleDir);
    await File.WriteAllBytesAsync(Path.Combine(staleDir, SetupName), [1, 2, 3]);
    Directory.SetLastWriteTimeUtc(staleDir, DateTime.UtcNow.AddDays(-10));

    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums()),
      [SetupUrl] = _ => Responses.Bytes(SetupBytes)
    };

    using var service = Service(routes);
    using var package = await DownloadAsync(service);

    Assert.True(File.Exists(package.FilePath));
    Assert.True(Directory.Exists(staleDir)); // Ein Versuch räumt keine fremden Verzeichnisse ab.

    TemporaryUpdateWorkspace.TryCleanupStale(_testRoot, TimeSpan.FromDays(2));
    Assert.False(Directory.Exists(staleDir));
    Assert.True(Directory.Exists(package.WorkspaceDirectory)); // Frisch bleibt bestehen.
  }

  // --- Prüfsummenfälle ------------------------------------------------------

  [Fact]
  public async Task MissingChecksumsUrl_BlocksDownload()
  {
    using var routes = new RouteHandler();
    using var service = Service(routes);

    var ex = await Assert.ThrowsAsync<UpdateSecurityException>(
      () => DownloadAsync(service, Update(checksumsUrl: null)));
    Assert.Contains("SHA256SUMS", ex.Message);
    Assert.Equal(0, routes.RequestCount); // Es wird gar nicht erst heruntergeladen.
  }

  [Fact]
  public async Task MissingSetupLine_BlocksAndCleansUp()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums(name: "OtherFile.zip")),
      [SetupUrl] = _ => Responses.Bytes(SetupBytes)
    };

    using var service = Service(routes);
    await Assert.ThrowsAsync<UpdateSecurityException>(() => DownloadAsync(service));
    AssertNoWorkspaceLeftBehind();
  }

  [Fact]
  public async Task WrongHash_BlocksAndCleansUp()
  {
    var wrongHash = new string('a', 64);
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums(hash: wrongHash)),
      [SetupUrl] = _ => Responses.Bytes(SetupBytes)
    };

    using var service = Service(routes);
    var ex = await Assert.ThrowsAsync<UpdateSecurityException>(() => DownloadAsync(service));
    Assert.Contains("SHA256", ex.Message);
    AssertNoWorkspaceLeftBehind();
  }

  [Fact]
  public async Task InvalidHashInChecksums_Blocks()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text($"nothex  {SetupName}\n"),
      [SetupUrl] = _ => Responses.Bytes(SetupBytes)
    };

    using var service = Service(routes);
    await Assert.ThrowsAsync<UpdateSecurityException>(() => DownloadAsync(service));
    AssertNoWorkspaceLeftBehind();
  }

  [Fact]
  public async Task ConflictingChecksumLines_Block()
  {
    var conflicting = DefaultChecksums() + DefaultChecksums(hash: new string('b', 64));
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(conflicting),
      [SetupUrl] = _ => Responses.Bytes(SetupBytes)
    };

    using var service = Service(routes);
    await Assert.ThrowsAsync<UpdateSecurityException>(() => DownloadAsync(service));
  }

  [Fact]
  public async Task DuplicateIdenticalChecksumLines_Block()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums() + DefaultChecksums()),
      [SetupUrl] = _ => Responses.Bytes(SetupBytes)
    };

    using var service = Service(routes);
    await Assert.ThrowsAsync<UpdateSecurityException>(() => DownloadAsync(service));
    AssertNoWorkspaceLeftBehind();
  }

  [Fact]
  public async Task ChecksumsOverallTimeout_BlocksAndCleansUp()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Stream(new ScriptedStream(
        [], ct => Task.Delay(Timeout.Infinite, ct))),
      [SetupUrl] = _ => Responses.Bytes(SetupBytes)
    };

    using var service = Service(routes, overallChecksumsTimeout: TimeSpan.FromMilliseconds(100));
    var ex = await Assert.ThrowsAsync<UpdateDownloadException>(() => DownloadAsync(service));
    Assert.Contains("Timed out", ex.Message);
    AssertNoWorkspaceLeftBehind();
  }

  [Fact]
  public async Task ChecksumsDownloadFailure_Blocks()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => new HttpResponseMessage(HttpStatusCode.NotFound),
      [SetupUrl] = _ => Responses.Bytes(SetupBytes)
    };

    using var service = Service(routes);
    await Assert.ThrowsAsync<UpdateDownloadException>(() => DownloadAsync(service));
  }

  [Fact]
  public async Task OversizedChecksumsFile_Blocks()
  {
    var huge = DefaultChecksums() + new string('x', 128 * 1024);
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(huge),
      [SetupUrl] = _ => Responses.Bytes(SetupBytes)
    };

    using var service = Service(routes);
    await Assert.ThrowsAsync<UpdateDownloadException>(() => DownloadAsync(service));
  }

  [Fact]
  public async Task InvalidSetupFileName_Blocks()
  {
    using var routes = new RouteHandler();
    using var service = Service(routes);

    // Pfad-Traversal, fehlendes Setup-Suffix und unerlaubte Zeichen
    await Assert.ThrowsAsync<UpdateSecurityException>(
      () => DownloadAsync(service, Update(assetName: @"..\evil-setup.exe")));
    await Assert.ThrowsAsync<UpdateSecurityException>(
      () => DownloadAsync(service, Update(assetName: "evil.exe")));
    await Assert.ThrowsAsync<UpdateSecurityException>(
      () => DownloadAsync(service, Update(assetName: "a b-setup.exe")));
  }

  [Fact]
  public async Task SetupAssetNameMustMatchOriginalUrlFileNameExactly()
  {
    const string mismatchedUrl = "https://github.com/Kentarohakase/YTMPresence/releases/download/v9.9.9/Other-setup.exe";
    using var routes = new RouteHandler();
    using var service = Service(routes);

    await Assert.ThrowsAsync<UpdateSecurityException>(
      () => DownloadAsync(service, Update(setupUrl: mismatchedUrl)));
    Assert.Equal(0, routes.RequestCount);
  }

  [Fact]
  public async Task ChecksumsUrlMustUseExactAssetFileName()
  {
    const string mismatchedUrl = "https://github.com/Kentarohakase/YTMPresence/releases/download/v9.9.9/sha256sums.txt";
    using var routes = new RouteHandler();
    using var service = Service(routes);

    await Assert.ThrowsAsync<UpdateSecurityException>(
      () => DownloadAsync(service, Update(checksumsUrl: mismatchedUrl)));
    Assert.Equal(0, routes.RequestCount);
  }

  // --- Downloadfälle --------------------------------------------------------

  [Fact]
  public async Task ContentLengthOverLimit_AbortsBeforeStreaming()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums()),
      [SetupUrl] = _ => Responses.BytesWithClaimedLength(SetupBytes, claimedLength: 100 * 1024 * 1024)
    };

    using var service = Service(routes, maxSetupBytes: 1024 * 1024);
    var ex = await Assert.ThrowsAsync<UpdateDownloadException>(() => DownloadAsync(service));
    Assert.Contains("limit", ex.Message);
    AssertNoWorkspaceLeftBehind();
  }

  [Fact]
  public async Task StreamWithoutContentLengthOverLimit_AbortsMidStream()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums()),
      [SetupUrl] = _ => Responses.UnknownLengthBytes(SetupBytes)
    };

    using var service = Service(routes, maxSetupBytes: 16 * 1024);
    var ex = await Assert.ThrowsAsync<UpdateDownloadException>(() => DownloadAsync(service));
    Assert.Contains("size limit", ex.Message);
    AssertNoWorkspaceLeftBehind();
  }

  [Fact]
  public async Task CancellationDuringDownload_CleansUp()
  {
    using var cts = new CancellationTokenSource();
    var firstChunkSent = new TaskCompletionSource();

    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums()),
      [SetupUrl] = _ => Responses.Stream(new ScriptedStream(
        [SetupBytes[..1024]],
        afterChunks: ct => { firstChunkSent.TrySetResult(); return Task.Delay(Timeout.Infinite, ct); }))
    };

    using var service = Service(routes);
    var download = DownloadAsync(service, ct: cts.Token);

    await firstChunkSent.Task;
    cts.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => download);
    AssertNoWorkspaceLeftBehind();
  }

  [Fact]
  public async Task NetworkErrorDuringDownload_CleansUp()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums()),
      [SetupUrl] = _ => Responses.Stream(new ScriptedStream(
        [SetupBytes[..1024]],
        afterChunks: _ => throw new IOException("connection reset")))
    };

    using var service = Service(routes);
    var ex = await Assert.ThrowsAsync<UpdateDownloadException>(() => DownloadAsync(service));
    Assert.Contains("interrupted", ex.Message);
    AssertNoWorkspaceLeftBehind();
  }

  [Fact]
  public async Task IncompleteStream_IsRejected()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums()),
      // Content-Length verspricht alles, der Stream endet aber vorzeitig.
      [SetupUrl] = _ => Responses.TruncatedBytes(SetupBytes, deliverOnly: 1024)
    };

    using var service = Service(routes);
    var ex = await Assert.ThrowsAsync<UpdateDownloadException>(() => DownloadAsync(service));
    Assert.Contains("incomplete", ex.Message);
    AssertNoWorkspaceLeftBehind();
  }

  [Fact]
  public async Task HttpErrorOnSetup_Blocks()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums()),
      [SetupUrl] = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
    };

    using var service = Service(routes);
    var ex = await Assert.ThrowsAsync<UpdateDownloadException>(() => DownloadAsync(service));
    Assert.Contains("500", ex.Message);
  }

  // --- URL- und Redirect-Fälle ---------------------------------------------

  [Fact]
  public async Task HttpSetupUrlInProduction_Blocks()
  {
    using var routes = new RouteHandler();
    using var service = Service(routes);

    await Assert.ThrowsAsync<UpdateSecurityException>(
      () => DownloadAsync(service, Update(setupUrl: "http://github.com/x/" + SetupName)));
    Assert.Equal(0, routes.RequestCount);
  }

  [Fact]
  public async Task LoopbackHttpInTestMode_Works()
  {
    const string localSetup = "http://127.0.0.1:18111/assets/" + SetupName;
    const string localChecksums = "http://127.0.0.1:18111/assets/SHA256SUMS.txt";

    using var routes = new RouteHandler
    {
      [localChecksums] = _ => Responses.Text(DefaultChecksums()),
      [localSetup] = _ => Responses.Bytes(SetupBytes)
    };

    using var service = Service(routes, allowLoopback: true);
    using var package = await DownloadAsync(
      service, Update(setupUrl: localSetup, checksumsUrl: localChecksums));

    Assert.Equal(SetupHash, package.Sha256);
  }

  [Fact]
  public async Task RedirectToCdn_IsFollowedAndVerified()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums()),
      [SetupUrl] = _ => Responses.Redirect(CdnUrl),
      [CdnUrl] = _ => Responses.Bytes(SetupBytes)
    };

    using var service = Service(routes);
    using var package = await DownloadAsync(service);
    Assert.Equal(SetupHash, package.Sha256);
  }

  [Fact]
  public async Task RelativeRedirect_IsResolvedAgainstCurrentUrl()
  {
    var relativeTarget = "https://github.com/relative/" + SetupName;
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums()),
      [SetupUrl] = _ => Responses.RedirectRelative("/relative/" + SetupName),
      [relativeTarget] = _ => Responses.Bytes(SetupBytes)
    };

    using var service = Service(routes);
    using var package = await DownloadAsync(service);
    Assert.Equal(SetupHash, package.Sha256);
  }

  [Theory]
  [InlineData("http://release-assets.githubusercontent.com/x")] // Downgrade auf HTTP
  [InlineData("http://127.0.0.1:8080/x")]                       // Loopback
  [InlineData("https://10.0.0.5/x")]                            // private IP
  [InlineData("https://evil.example/x")]                        // fremder Host
  [InlineData("file:///C:/evil.exe")]                           // file:
  public async Task RedirectToForbiddenTarget_Blocks(string redirectTarget)
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums()),
      [SetupUrl] = _ => Responses.Redirect(redirectTarget)
    };

    using var service = Service(routes);
    var ex = await Assert.ThrowsAsync<UpdateSecurityException>(() => DownloadAsync(service));
    Assert.Contains("Redirect", ex.Message);
    AssertNoWorkspaceLeftBehind();
  }

  [Fact]
  public async Task RedirectLoop_IsDetected()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums()),
      [SetupUrl] = _ => Responses.Redirect(CdnUrl),
      [CdnUrl] = _ => Responses.Redirect(SetupUrl)
    };

    using var service = Service(routes);
    var ex = await Assert.ThrowsAsync<UpdateSecurityException>(() => DownloadAsync(service));
    Assert.Contains("loop", ex.Message);
  }

  [Fact]
  public async Task TooManyRedirects_Block()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums())
    };

    for (var i = 0; i < 10; i++)
    {
      var from = i == 0 ? SetupUrl : $"{CdnUrl}/hop{i}";
      var to = $"{CdnUrl}/hop{i + 1}";
      routes[from] = _ => Responses.Redirect(to);
    }

    using var service = Service(routes, maxRedirects: 3);
    var ex = await Assert.ThrowsAsync<UpdateSecurityException>(() => DownloadAsync(service));
    Assert.Contains("Too many redirects", ex.Message);
  }

  [Fact]
  public async Task RedirectWithoutLocation_Blocks()
  {
    using var routes = new RouteHandler
    {
      [ChecksumsUrl] = _ => Responses.Text(DefaultChecksums()),
      [SetupUrl] = _ => new HttpResponseMessage(HttpStatusCode.Found) // ohne Location
    };

    using var service = Service(routes);
    var ex = await Assert.ThrowsAsync<UpdateSecurityException>(() => DownloadAsync(service));
    Assert.Contains("Location", ex.Message);
  }

  [Fact]
  public async Task RedirectToNonLoopbackInTestMode_Blocks()
  {
    const string localSetup = "http://127.0.0.1:18111/assets/" + SetupName;
    const string localChecksums = "http://127.0.0.1:18111/assets/SHA256SUMS.txt";

    using var routes = new RouteHandler
    {
      [localChecksums] = _ => Responses.Text(DefaultChecksums()),
      [localSetup] = _ => Responses.Redirect("http://192.168.0.10/" + SetupName)
    };

    using var service = Service(routes, allowLoopback: true);
    await Assert.ThrowsAsync<UpdateSecurityException>(
      () => DownloadAsync(service, Update(setupUrl: localSetup, checksumsUrl: localChecksums)));
  }

  private void AssertNoWorkspaceLeftBehind()
  {
    if (!Directory.Exists(_testRoot))
      return;

    // Nur bewusst angelegte Test-Fixtures (z. B. "stale-attempt") dürfen übrig sein.
    var leftovers = Directory.GetDirectories(_testRoot)
      .Where(d => !Path.GetFileName(d).StartsWith("stale-", StringComparison.Ordinal))
      .ToList();

    Assert.Empty(leftovers);
  }
}
