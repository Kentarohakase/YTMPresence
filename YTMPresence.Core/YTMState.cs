using System.Text.Json.Serialization;

namespace YTMPresence.Core;

public sealed record YtmState(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("album")] string? Album,
    [property: JsonPropertyName("albumArtUrl")] string? AlbumArtUrl,
    [property: JsonPropertyName("isPlaying")] bool IsPlaying,
    [property: JsonPropertyName("position")] double? PositionSeconds,
    [property: JsonPropertyName("duration")] double? DurationSeconds,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("shareUrl")] string? ShareUrl,
    [property: JsonPropertyName("isAd")] bool IsAd,
    [property: JsonPropertyName("ts")] long TimestampUnixMs,
    [property: JsonPropertyName("token")] string? Token = null
);
