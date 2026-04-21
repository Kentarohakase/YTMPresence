namespace YTMPresence.Core;

public sealed record CompanionStatus(
    bool IsRunning,
    int ConnectedClients,
    DateTimeOffset? LastMessageUtc,
    string? LastTitle,
    string? LastArtist,
    bool? LastIsPlaying,
    int UnauthorizedMessages,
    DateTimeOffset? LastUnauthorizedUtc,
    bool DiscordOk,
    string? LastDiscordError,
    DateTimeOffset? LastDiscordOkUtc
);