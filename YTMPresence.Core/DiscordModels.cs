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

/// <summary>
/// Was Discord auf einen Befehl geantwortet hat. Erst die Antwort sagt, ob
/// die Presence wirklich gesetzt wurde; das Schreiben des Frames allein nicht.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DiscordCommandOutcome>))]
public enum DiscordCommandOutcome
{
  Accepted,
  Rejected,
  NoReply
}

public sealed record DiscordCommandResult(
    DiscordCommandOutcome Outcome,
    int? ErrorCode = null,
    string? ErrorMessage = null)
{
  public static DiscordCommandResult Accepted { get; } = new(DiscordCommandOutcome.Accepted);
  public static DiscordCommandResult NoReply { get; } = new(DiscordCommandOutcome.NoReply);

  public static DiscordCommandResult Rejected(int? code, string? message)
    => new(DiscordCommandOutcome.Rejected, code, message);
}