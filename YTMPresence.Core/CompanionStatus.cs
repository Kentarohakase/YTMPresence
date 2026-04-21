namespace YTMPresence.Core;

public sealed record CompanionStatus(
    bool IsRunning,
    int ConnectedClients,
    DateTimeOffset? LastMessageUtc,
    string? LastTitle,
    string? LastArtist,
    string? LastAlbum,
    string? LastAlbumArtUrl,
    string? LastTrackUrl,
    bool? LastIsPlaying,
    double? LastPositionSeconds,
    double? LastDurationSeconds,
    int UnauthorizedMessages,
    DateTimeOffset? LastUnauthorizedUtc,
    bool DiscordOk,
    string? LastDiscordError,
    DateTimeOffset? LastDiscordOkUtc
);
