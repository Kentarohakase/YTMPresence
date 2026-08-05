using System.Text.Json;
using YTMPresence.Core;

namespace YTMPresence.Core.Tests;

public class SettingsStoreTests : IDisposable
{
  private readonly string _dir;
  private readonly string _path;

  public SettingsStoreTests()
  {
    _dir = Path.Combine(Path.GetTempPath(), "ytmp-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(_dir);
    _path = Path.Combine(_dir, "settings.json");
  }

  public void Dispose()
  {
    try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
  }

  [Fact]
  public void LoadOrCreateDefault_CreatesFileWithToken()
  {
    var settings = SettingsStore.LoadOrCreateDefault(_path);

    Assert.True(File.Exists(_path));
    Assert.False(string.IsNullOrWhiteSpace(settings.SecurityToken));
  }

  [Fact]
  public void SaveThenLoad_RoundtripsValues()
  {
    var original = new AppSettings
    {
      DiscordClientId = "123456",
      ListenPort = 28000,
      WebSocketPath = "/socket",
      SecurityToken = "fixed-token"
    };

    SettingsStore.Save(_path, original);
    var loaded = SettingsStore.LoadOrCreateDefault(_path);

    Assert.Equal("123456", loaded.DiscordClientId);
    Assert.Equal(28000, loaded.ListenPort);
    Assert.Equal("/socket", loaded.WebSocketPath);
    Assert.Equal("fixed-token", loaded.SecurityToken);
  }

  [Fact]
  public void Normalize_ClampsPortAndIntervals()
  {
    var settings = new AppSettings
    {
      ListenPort = 999_999,
      MinDiscordUpdateSeconds = 1,
      IdleClearSeconds = 1,
    };

    SettingsStore.Save(_path, settings);
    var loaded = SettingsStore.LoadOrCreateDefault(_path);

    Assert.Equal(65535, loaded.ListenPort);
    Assert.Equal(5, loaded.MinDiscordUpdateSeconds);
    Assert.Equal(10, loaded.IdleClearSeconds);
  }

  [Fact]
  public void Normalize_FixesEmptyHostAndPath()
  {
    var settings = new AppSettings
    {
      ListenHost = "   ",
      WebSocketPath = "ws"
    };

    SettingsStore.Save(_path, settings);
    var loaded = SettingsStore.LoadOrCreateDefault(_path);

    Assert.Equal("127.0.0.1", loaded.ListenHost);
    Assert.Equal("/ws", loaded.WebSocketPath);
  }

  [Fact]
  public void Normalize_GeneratesTokenWhenEmpty()
  {
    var settings = new AppSettings { SecurityToken = "" };

    SettingsStore.Save(_path, settings);

    Assert.False(string.IsNullOrWhiteSpace(settings.SecurityToken));
  }

  [Fact]
  public void LoadOrCreateDefault_CorruptedJsonIsPreservedAndRecovered()
  {
    const string corruptedJson = "{ this is not valid json";
    File.WriteAllText(_path, corruptedJson);

    var settings = SettingsStore.LoadOrCreateDefault(_path);

    Assert.False(string.IsNullOrWhiteSpace(settings.SecurityToken));

    using var document = JsonDocument.Parse(File.ReadAllText(_path));
    Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);

    var backups = Directory.GetFiles(_dir, "settings.corrupt-*.json");
    var backup = Assert.Single(backups);
    Assert.Equal(corruptedJson, File.ReadAllText(backup));
  }

  [Fact]
  public void Save_CreatesMissingParentDirectoryAndLeavesNoTempFile()
  {
    var nestedPath = Path.Combine(_dir, "nested", "config", "settings.json");

    SettingsStore.Save(nestedPath, new AppSettings { SecurityToken = "fixed-token" });

    Assert.True(File.Exists(nestedPath));
    Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(nestedPath)!, "*.tmp"));
    using var document = JsonDocument.Parse(File.ReadAllText(nestedPath));
    Assert.Equal(
        "fixed-token",
        document.RootElement
            .GetProperty("SecurityToken")
            .GetString());
  }

  [Fact]
  public void Save_ReplacesExistingFileWithValidJson()
  {
    SettingsStore.Save(_path, new AppSettings { SecurityToken = "first-token" });
    SettingsStore.Save(_path, new AppSettings { SecurityToken = "second-token" });

    using var document = JsonDocument.Parse(File.ReadAllText(_path));
    Assert.Equal("second-token", document.RootElement.GetProperty("SecurityToken").GetString());
    Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
  }
}
