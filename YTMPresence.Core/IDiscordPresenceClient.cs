namespace YTMPresence.Core;

/// <summary>
/// Abstraktion über den Discord-IPC-Client, damit die Presence-Logik
/// unabhängig vom echten Discord-Prozess getestet werden kann.
/// </summary>
public interface IDiscordPresenceClient
{
  Task EnsureConnectedAsync(CancellationToken ct);
  Task SetActivityAsync(DiscordActivity activity, CancellationToken ct);
  Task ClearActivityAsync(CancellationToken ct);
}
