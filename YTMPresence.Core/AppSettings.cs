namespace YTMPresence.Core;

public enum AdBehavior
{
  ShowAdvertisement = 0,
  Ignore = 1,
  Clear = 2
}

public sealed class DiscordAssetKeys
{
  public string LargeImageKey { get; set; } = "ytm_large";
  public string LargeImageText { get; set; } = "YouTube Music";
  public string PlaySmallImageKey { get; set; } = "play";
  public string PauseSmallImageKey { get; set; } = "pause";
}

public sealed class PlayerWindowSettings
{
  public bool AlwaysOnTop { get; set; }
  public double? Left { get; set; }
  public double? Top { get; set; }
  public double Width { get; set; } = 520;
  public double Height { get; set; } = 250;
}

public sealed class AppSettings
{
  public string DiscordClientId { get; set; } = "1476649347008565259";

  public string ListenHost { get; set; } = "127.0.0.1";
  public int ListenPort { get; set; } = 17373;
  public string WebSocketPath { get; set; } = "/ws";

  public int MinDiscordUpdateSeconds { get; set; } = 15;
  public int IdleClearSeconds { get; set; } = 120;
  public int PausedClearSeconds { get; set; } = 120;
  public bool OnlyShowWhenPlaying { get; set; } = false;

  public AdBehavior AdBehavior { get; set; } = AdBehavior.ShowAdvertisement;
  public DiscordAssetKeys Assets { get; set; } = new();
  public PlayerWindowSettings PlayerWindow { get; set; } = new();

  /// <summary>
  /// Shared Secret: muss von Extension mitgesendet werden.
  /// Wird automatisch generiert, wenn leer.
  /// </summary>
  public string SecurityToken { get; set; } = "";

  public Uri GetListenUrl() => new($"http://{ListenHost}:{ListenPort}");
  public string GetWebSocketEndpoint() => $"ws://{ListenHost}:{ListenPort}{WebSocketPath}";
}
