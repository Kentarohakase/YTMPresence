using System.ComponentModel;
using System.Text.Json.Serialization;

namespace YTMPresence.Core;

public enum DiscordActivityType
{
  Playing = 0,
  Streaming = 1,
  Listening = 2,
  Watching = 3,
  Custom = 4,
  Competing = 5
}
public sealed record DiscordActivity(
    [property: JsonPropertyName("details")] string? Details,
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("timestamps")] DiscordTimestamps? Timestamps = null,
    [property: JsonPropertyName("assets")] DiscordAssets? Assets = null,
    [property: JsonPropertyName("buttons")] DiscordButton[]? Buttons = null,
    [property: JsonPropertyName("type")] DiscordActivityType Type = DiscordActivityType.Listening);

public sealed record DiscordTimestamps(
    [property: JsonPropertyName("start")] long? StartUnixSeconds,
    [property: JsonPropertyName("end")] long? EndUnixSeconds);

public sealed record DiscordAssets(
    [property: JsonPropertyName("large_image")] string? LargeImage,
    [property: JsonPropertyName("large_text")] string? LargeText,
    [property: JsonPropertyName("small_image")] string? SmallImage,
    [property: JsonPropertyName("small_text")] string? SmallText);

public sealed record DiscordButton(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("url")] string Url);