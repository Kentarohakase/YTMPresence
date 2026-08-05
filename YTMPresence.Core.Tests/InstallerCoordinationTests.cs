namespace YTMPresence.Core.Tests;

public class InstallerCoordinationTests
{
  [Fact]
  public void InstanceKey_UsesExpectedNormalizedUtf16Sha256()
  {
    var key = InstallerCoordination.GetInstanceKey(@"C:\Users\Kenta\AppData\Local\Programs\YTMPresence\app");
    Assert.Equal("229237f93d35110457aaa7122614dcabd493e49146361ddeac57b0cd3aa5d13d", key);
  }

  [Fact]
  public void InstanceKey_TrimsTrailingSeparators()
  {
    var withSlash = InstallerCoordination.GetInstanceKey(@"C:\Programs\YTMPresence\app\");
    var withoutSlash = InstallerCoordination.GetInstanceKey(@"C:\Programs\YTMPresence\app");
    Assert.Equal(withoutSlash, withSlash);
  }

  [Fact]
  public void InstanceKey_DoesNotCollapseDifferentPunctuationOrUnicode()
  {
    var hyphen = InstallerCoordination.GetInstanceKey(@"C:\Tools\YTM-Presence\app");
    var underscore = InstallerCoordination.GetInstanceKey(@"C:\Tools\YTM_Presence\app");
    var unicode = InstallerCoordination.GetInstanceKey(@"C:\Users\Müller\app");

    Assert.NotEqual(hyphen, underscore);
    Assert.NotEqual(unicode, InstallerCoordination.GetInstanceKey(@"C:\Users\M_ller\app"));
    Assert.All([hyphen, underscore, unicode], key => Assert.Matches("^[0-9a-f]{64}$", key));
  }

  [Fact]
  public void InstanceKey_DiffersForDifferentDirectories()
  {
    var installed = InstallerCoordination.GetInstanceKey(@"C:\Users\K\AppData\Local\Programs\YTMPresence\app");
    var portable = InstallerCoordination.GetInstanceKey(@"D:\Tools\YTMPresence\app");
    Assert.NotEqual(installed, portable);
  }

  [Fact]
  public void ObjectNames_UseSessionLocalNamespaceAndPrefixes()
  {
    const string dir = @"C:\Programs\YTMPresence\app";
    Assert.StartsWith(@"Local\YTMPresence.Running.", InstallerCoordination.GetRunningMutexName(dir));
    Assert.StartsWith(@"Local\YTMPresence.Shutdown.", InstallerCoordination.GetShutdownEventName(dir));
  }

  [Fact]
  public void EmptyDirectory_Throws()
  {
    Assert.Throws<ArgumentException>(() => InstallerCoordination.GetInstanceKey(""));
  }
}
