using System.Text.Json;
using YTMPresence.Core;

namespace YTMPresence.Core.Tests;

public class DiscordIpcPayloadTests
{
  [Fact]
  public void ClearActivity_UsesSetActivityWithNullActivity()
  {
    // Discords RPC kennt kein CLEAR_ACTIVITY. Der Befehl wurde jahrelang
    // gesendet, von Discord abgelehnt und die Ablehnung verworfen - die
    // Presence blieb stehen, obwohl die App "Discord OK" meldete.
    var json = DiscordIpcClient.BuildClearActivityJson("abc123");

    using var doc = JsonDocument.Parse(json);
    var root = doc.RootElement;

    Assert.Equal("SET_ACTIVITY", root.GetProperty("cmd").GetString());

    var args = root.GetProperty("args");
    Assert.True(args.TryGetProperty("activity", out var activity),
        "Das Feld 'activity' muss vorhanden sein - ohne es loescht Discord nichts.");
    Assert.Equal(JsonValueKind.Null, activity.ValueKind);
  }

  [Fact]
  public void ClearActivity_KeepsNullInJsonDespiteNullIgnoringOptions()
  {
    // Die Serialisierungsoptionen des Clients lassen Null-Felder weg. Wuerde
    // die Nachricht darueber gebaut, fiele ausgerechnet 'activity: null' raus
    // und der Befehl haette keine Wirkung.
    var json = DiscordIpcClient.BuildClearActivityJson("abc123");

    Assert.Contains("\"activity\":null", json.Replace(" ", ""));
  }

  [Fact]
  public void SetActivity_SendsStatusDisplayTypeAsNumber()
  {
    var activity = new DiscordActivity("Title", "Artist", StatusDisplayType: DiscordStatusDisplayType.Details);

    using var doc = JsonDocument.Parse(DiscordIpcClient.BuildSetActivityJson(activity, "abc123"));
    var sent = doc.RootElement.GetProperty("args").GetProperty("activity");

    Assert.Equal(2, sent.GetProperty("status_display_type").GetInt32());
  }

  [Fact]
  public void SetActivity_OmitsStatusDisplayTypeWhenNotSet()
  {
    // Ohne das Feld nimmt Discord den Anwendungsnamen, wie vor der Option.
    var activity = new DiscordActivity("Title", "Artist");

    using var doc = JsonDocument.Parse(DiscordIpcClient.BuildSetActivityJson(activity, "abc123"));
    var sent = doc.RootElement.GetProperty("args").GetProperty("activity");

    Assert.False(sent.TryGetProperty("status_display_type", out _));
  }

  [Fact]
  public void SetActivity_SendsTheTitleLinkAsDetailsUrl()
  {
    var activity = new DiscordActivity("Title", "Artist", DetailsUrl: "https://music.youtube.com/watch?v=abc123");

    using var doc = JsonDocument.Parse(DiscordIpcClient.BuildSetActivityJson(activity, "abc123"));
    var sent = doc.RootElement.GetProperty("args").GetProperty("activity");

    Assert.Equal("https://music.youtube.com/watch?v=abc123", sent.GetProperty("details_url").GetString());
  }

  [Fact]
  public void SetActivity_OmitsDetailsUrlWhenNotSet()
  {
    var activity = new DiscordActivity("Title", "Artist");

    using var doc = JsonDocument.Parse(DiscordIpcClient.BuildSetActivityJson(activity, "abc123"));
    var sent = doc.RootElement.GetProperty("args").GetProperty("activity");

    Assert.False(sent.TryGetProperty("details_url", out _));
  }

  [Fact]
  public void SetActivity_SendsTheActivityName()
  {
    var activity = new DiscordActivity("Title", "Artist", Name: "YouTube Music");

    using var doc = JsonDocument.Parse(DiscordIpcClient.BuildSetActivityJson(activity, "abc123"));
    var sent = doc.RootElement.GetProperty("args").GetProperty("activity");

    Assert.Equal("YouTube Music", sent.GetProperty("name").GetString());
  }

  [Fact]
  public void SetActivity_OmitsTheActivityNameWhenNotSet()
  {
    var activity = new DiscordActivity("Title", "Artist");

    using var doc = JsonDocument.Parse(DiscordIpcClient.BuildSetActivityJson(activity, "abc123"));
    var sent = doc.RootElement.GetProperty("args").GetProperty("activity");

    Assert.False(sent.TryGetProperty("name", out _));
  }

  [Fact]
  public void ClearActivity_CarriesProcessIdAndTheGivenNonce()
  {
    // Die Nonce ordnet Discords Antwort dem Befehl zu. Dass jeder Befehl eine
    // eigene bekommt, prueft DiscordIpcReplyTests ueber den ganzen Ablauf.
    var json = DiscordIpcClient.BuildClearActivityJson("abc123");

    using var doc = JsonDocument.Parse(json);
    var args = doc.RootElement.GetProperty("args");

    Assert.Equal(Environment.ProcessId, args.GetProperty("pid").GetInt32());
    Assert.Equal("abc123", doc.RootElement.GetProperty("nonce").GetString());
  }
}
