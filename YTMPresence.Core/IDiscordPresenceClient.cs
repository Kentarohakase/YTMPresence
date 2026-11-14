namespace YTMPresence.Core;

/// <summary>
/// Abstraktion über den Discord-IPC-Client, damit die Presence-Logik
/// unabhängig vom echten Discord-Prozess getestet werden kann.
/// </summary>
public interface IDiscordPresenceClient
{
  /// <summary>
  /// Die Verbindung steht und Discord hat READY gemeldet. Nach einem
  /// Verbindungsverlust, etwa bei einem Neustart von Discord, ist der Wert
  /// false, bis <see cref="EnsureConnectedAsync"/> neu verbunden hat.
  /// </summary>
  bool IsReady { get; }

  Task EnsureConnectedAsync(CancellationToken ct);
  Task<DiscordCommandResult> SetActivityAsync(DiscordActivity activity, CancellationToken ct);
  Task<DiscordCommandResult> ClearActivityAsync(CancellationToken ct);
}
