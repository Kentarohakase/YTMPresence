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

  public bool CheckForUpdatesOnStartup { get; set; } = true;
  public string UpdateApiUrl { get; set; } = "https://api.github.com/repos/Kentarohakase/YTMPresence/releases/latest";

  /// <summary>
  /// Zeitpunkt des letzten erfolgreich abgeschlossenen Update-Checks.
  ///
  /// Der Check beim Start meldet Fehler nur ins Log, damit nicht bei jedem
  /// Hochfahren ohne Netz eine Sprechblase erscheint. Dadurch kann er aber
  /// monatelang scheitern, ohne dass es jemand bemerkt - etwa hinter einem
  /// Proxy, bei blockierender Firewall oder bei API-Ratenbegrenzung. Dieser
  /// Zeitstempel macht das im Diagnosefenster sichtbar.
  /// </summary>
  public DateTimeOffset? LastSuccessfulUpdateCheckUtc { get; set; }

  /// <summary>
  /// Nur für automatisierte Update-Tests (scripts/test-update-flow.ps1):
  /// erlaubt HTTP-Updatequellen auf Loopback-Adressen. In Produktion muss
  /// dieser Wert false bleiben; reale Updates akzeptieren ausschließlich
  /// HTTPS zu den erlaubten GitHub-Hosts.
  /// </summary>
  public bool AllowLoopbackUpdateSource { get; set; }

  public bool HasSeenOnboarding { get; set; }

  /// <summary>
  /// Shared Secret: muss von Extension mitgesendet werden.
  /// Wird automatisch generiert, wenn leer.
  /// </summary>
  public string SecurityToken { get; set; } = "";

  public Uri GetListenUrl() => new($"http://{FormatHostForUri(ListenHost)}:{ListenPort}");
  public string GetWebSocketEndpoint() => $"ws://{FormatHostForUri(ListenHost)}:{ListenPort}{WebSocketPath}";

  private static string FormatHostForUri(string host)
  {
    if (System.Net.IPAddress.TryParse(host, out var address) &&
        address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 &&
        !host.StartsWith('['))
    {
      return $"[{host}]";
    }

    return host;
  }
}
