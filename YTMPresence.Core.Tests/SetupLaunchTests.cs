using System.Security.Cryptography;

using YTMPresence.Core.Updates;

namespace YTMPresence.Core.Tests;

public class SetupLaunchTests : IDisposable
{
  private const string SetupName = "YTMPresence-9.9.9-win-x64-setup.exe";

  private readonly string _testRoot = Path.Combine(
    Path.GetTempPath(), "YTMPresence.Tests", Path.GetRandomFileName());

  public void Dispose()
  {
    try
    {
      if (Directory.Exists(_testRoot))
        Directory.Delete(_testRoot, recursive: true);
    }
    catch
    {
      // Best effort.
    }
  }

  private VerifiedSetupPackage CreatePackage(
    byte[]? content = null,
    string? recordedHash = null,
    long? recordedLength = null,
    long maxAllowedBytes = 300L * 1024 * 1024,
    bool writeFile = true)
  {
    content ??= [1, 2, 3, 4, 5, 6, 7, 8];

    var workspace = TemporaryUpdateWorkspace.Create(_testRoot);
    var filePath = Path.Combine(workspace.Directory, SetupName);

    if (writeFile)
      File.WriteAllBytes(filePath, content);

    var hash = recordedHash ?? Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    return new VerifiedSetupPackage(
      filePath, SetupName, hash, recordedLength ?? content.LongLength, workspace, maxAllowedBytes);
  }

  [Fact]
  public async Task VerifiedPackage_IsLaunched()
  {
    using var package = CreatePackage();
    string? startedPath = null;

    var result = await UpdateInstaller.LaunchVerifiedSetupAsync(package, path =>
    {
      startedPath = path;
      return true;
    });

    Assert.True(result);
    Assert.Equal(package.FilePath, startedPath);
    Assert.True(Path.IsPathRooted(startedPath));
  }

  [Fact]
  public async Task FileIsWriteLockedDuringLaunch()
  {
    using var package = CreatePackage();

    await UpdateInstaller.LaunchVerifiedSetupAsync(package, path =>
    {
      // Während des Starts darf niemand die Datei verändern oder löschen.
      Assert.ThrowsAny<IOException>(() =>
        new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None).Dispose());
      return true;
    });

    // Nach dem Start ist die Sperre wieder frei.
    await using var writable = new FileStream(package.FilePath, FileMode.Open, FileAccess.Write, FileShare.None);
  }

  [Fact]
  public async Task MissingHash_BlocksLaunch()
  {
    using var package = CreatePackage(recordedHash: "");
    var started = false;

    await Assert.ThrowsAsync<UpdateSecurityException>(
      () => UpdateInstaller.LaunchVerifiedSetupAsync(package, _ => started = true));
    Assert.False(started);
  }

  [Fact]
  public async Task WrongHash_BlocksLaunch()
  {
    using var package = CreatePackage(recordedHash: new string('a', 64));
    var started = false;

    var ex = await Assert.ThrowsAsync<UpdateSecurityException>(
      () => UpdateInstaller.LaunchVerifiedSetupAsync(package, _ => started = true));
    Assert.Contains("modified", ex.Message);
    Assert.False(started);
  }

  [Fact]
  public async Task MissingFile_BlocksLaunch()
  {
    using var package = CreatePackage(writeFile: false);
    var started = false;

    var ex = await Assert.ThrowsAsync<UpdateSecurityException>(
      () => UpdateInstaller.LaunchVerifiedSetupAsync(package, _ => started = true));
    Assert.Contains("no longer exists", ex.Message);
    Assert.False(started);
  }

  [Fact]
  public async Task FileModifiedAfterVerification_BlocksLaunch()
  {
    using var package = CreatePackage(content: [1, 2, 3, 4]);

    // Gleiche Länge, anderer Inhalt: nur der Hash entlarvt die Manipulation.
    await File.WriteAllBytesAsync(package.FilePath, [9, 9, 9, 9]);

    var started = false;
    var ex = await Assert.ThrowsAsync<UpdateSecurityException>(
      () => UpdateInstaller.LaunchVerifiedSetupAsync(package, _ => started = true));
    Assert.Contains("SHA256", ex.Message);
    Assert.False(started);
  }

  [Fact]
  public async Task FileLengthChangedAfterVerification_BlocksLaunch()
  {
    using var package = CreatePackage(content: [1, 2, 3, 4]);
    await File.WriteAllBytesAsync(package.FilePath, [1, 2, 3, 4, 5]);

    var ex = await Assert.ThrowsAsync<UpdateSecurityException>(
      () => UpdateInstaller.LaunchVerifiedSetupAsync(package, _ => true));
    Assert.Contains("file size", ex.Message);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task NonPositiveRecordedLength_BlocksLaunch(long recordedLength)
  {
    using var package = CreatePackage(recordedLength: recordedLength);
    await Assert.ThrowsAsync<UpdateSecurityException>(
      () => UpdateInstaller.LaunchVerifiedSetupAsync(package, _ => true));
  }

  [Fact]
  public async Task RecordedLengthOverConfiguredLimit_BlocksLaunch()
  {
    using var package = CreatePackage(recordedLength: 8, maxAllowedBytes: 4);
    await Assert.ThrowsAsync<UpdateSecurityException>(
      () => UpdateInstaller.LaunchVerifiedSetupAsync(package, _ => true));
  }

  [Fact]
  public async Task RenamedFile_BlocksLaunch()
  {
    using var package = CreatePackage();
    var renamed = Path.Combine(Path.GetDirectoryName(package.FilePath)!, "Other-setup.exe");
    File.Move(package.FilePath, renamed);

    await Assert.ThrowsAsync<UpdateSecurityException>(
      () => UpdateInstaller.LaunchVerifiedSetupAsync(package, _ => true));
  }

  [Fact]
  public async Task StartReturningFalse_Throws_AndKeepsFileIntact()
  {
    using var package = CreatePackage();

    // Der Aufrufer (die App) beendet sich nur bei true; eine Exception hier
    // bedeutet: App bleibt benutzbar.
    var ex = await Assert.ThrowsAsync<UpdateDownloadException>(
      () => UpdateInstaller.LaunchVerifiedSetupAsync(package, _ => false));
    Assert.Contains("not started", ex.Message);
    Assert.True(File.Exists(package.FilePath));
  }

  [Fact]
  public async Task StartThrowing_IsWrapped()
  {
    using var package = CreatePackage();

    var ex = await Assert.ThrowsAsync<UpdateDownloadException>(
      () => UpdateInstaller.LaunchVerifiedSetupAsync(
        package, _ => throw new InvalidOperationException("kaputt")));
    Assert.IsType<InvalidOperationException>(ex.InnerException);
  }

  [Fact]
  public async Task LaunchLock_ReverifiesIndependently()
  {
    using var package = CreatePackage();

    using (var launchLock = await package.AcquireLaunchLockAsync())
    {
      Assert.NotNull(launchLock);
    }

    // Nach Freigabe kann die Datei wieder verändert werden – ein erneuter
    // Launch-Versuch muss die Manipulation dann erkennen.
    await File.WriteAllBytesAsync(package.FilePath, [0, 0, 0, 0, 0, 0, 0, 0]);
    await Assert.ThrowsAsync<UpdateSecurityException>(() => package.AcquireLaunchLockAsync());
  }

  [Fact]
  public void Dispose_RemovesWorkspace_UnlessKeepFiles()
  {
    var package = CreatePackage();
    var workspace = package.WorkspaceDirectory;
    package.Dispose();
    Assert.False(Directory.Exists(workspace));

    var kept = CreatePackage();
    kept.KeepFilesOnDispose = true;
    var keptWorkspace = kept.WorkspaceDirectory;
    kept.Dispose();
    Assert.True(Directory.Exists(keptWorkspace));
    Assert.True(File.Exists(kept.FilePath));
  }
}
