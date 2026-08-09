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
    var json = DiscordIpcClient.BuildClearActivityJson();

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
    var json = DiscordIpcClient.BuildClearActivityJson();

    Assert.Contains("\"activity\":null", json.Replace(" ", ""));
  }

  [Fact]
  public void ClearActivity_CarriesProcessIdAndUniqueNonce()
  {
    var first = DiscordIpcClient.BuildClearActivityJson();
    var second = DiscordIpcClient.BuildClearActivityJson();

    using var doc = JsonDocument.Parse(first);
    var args = doc.RootElement.GetProperty("args");

    Assert.Equal(Environment.ProcessId, args.GetProperty("pid").GetInt32());
    Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("nonce").GetString()));

    using var secondDoc = JsonDocument.Parse(second);
    Assert.NotEqual(
        doc.RootElement.GetProperty("nonce").GetString(),
        secondDoc.RootElement.GetProperty("nonce").GetString());
  }
}
