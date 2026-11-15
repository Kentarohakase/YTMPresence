using System.Runtime.CompilerServices;
using YTMPresence.Core;

namespace YTMPresence.Core.Tests;

internal static class TestLogSetup
{
  // Läuft, bevor ein Test das erste Mal protokolliert. Ohne die Umleitung
  // schrieben etwa die CompanionServer-Tests ihre Testserver auf zufälligen
  // Ports ins echte Log unter %APPDATA%\YTMPresence\logs.
  internal static readonly string LogDirectory =
      Path.Combine(Path.GetTempPath(), "YTMPresence.Tests", "logs");

  [ModuleInitializer]
  internal static void RedirectLogs() => Logger.InitializeAt(LogDirectory);
}

public class TestLogSetupTests
{
  [Fact]
  public void TestRuns_LogOutsideTheInstalledAppsLogFolder()
  {
    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    Assert.Equal(Path.GetFullPath(TestLogSetup.LogDirectory), Path.GetFullPath(Logger.GetLogDirectoryPath()));
    Assert.False(
        Logger.GetLogDirectoryPath().StartsWith(Path.Combine(appData, "YTMPresence"), StringComparison.OrdinalIgnoreCase),
        "Tests must not write into the installed app's log folder.");
  }
}
