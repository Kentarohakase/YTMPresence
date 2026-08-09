using System.Globalization;
using YTMPresence.Core;

namespace YTMPresence.Core.Tests;

public class PresenceTextTests
{
  private static T WithCulture<T>(string language, Func<T> read)
  {
    var original = CultureInfo.CurrentUICulture;
    try
    {
      CultureInfo.CurrentUICulture = new CultureInfo(language);
      return read();
    }
    finally
    {
      CultureInfo.CurrentUICulture = original;
    }
  }

  [Theory]
  [InlineData("de", "Läuft")]
  [InlineData("en", "Playing")]
  [InlineData("es", "Reproduciendo")]
  // Unbekannte Sprachen fallen auf Englisch zurueck, nicht auf Deutsch.
  [InlineData("fr", "Playing")]
  [InlineData("ja", "Playing")]
  public void Playing_FollowsUiCulture(string language, string expected)
  {
    Assert.Equal(expected, WithCulture(language, () => PresenceText.Playing));
  }

  [Theory]
  [InlineData("de", "Pausiert")]
  [InlineData("en", "Paused")]
  [InlineData("fr", "Paused")]
  public void Paused_FollowsUiCulture(string language, string expected)
  {
    Assert.Equal(expected, WithCulture(language, () => PresenceText.Paused));
  }

  [Fact]
  public void PausedAt_ContainsTimeAndArtist()
  {
    var text = WithCulture("en", () => PresenceText.PausedAt("1:23", "Sam Smith"));

    Assert.Contains("1:23", text);
    Assert.Contains("Sam Smith", text);
    Assert.DoesNotContain("Pausiert", text);
  }

  [Theory]
  [InlineData("de")]
  [InlineData("en")]
  [InlineData("es")]
  [InlineData("fr")]
  public void ButtonLabel_StaysWithinDiscordLimit(string language)
  {
    // Discord begrenzt Button-Beschriftungen auf 32 Zeichen und lehnt sonst
    // die gesamte Aktivitaet ab - genau wie bei einer ungueltigen URL.
    var label = WithCulture(language, () => PresenceText.OpenOnYouTubeMusic);

    Assert.NotEmpty(label);
    Assert.True(label.Length <= 32, $"'{label}' ist mit {label.Length} Zeichen zu lang.");
  }

  [Fact]
  public void UnknownCulture_DoesNotFallBackToGerman()
  {
    // Der Presence-Text wird von fremden Nutzern gelesen. Englisch ist die
    // sinnvollere Rueckfallebene als die Muttersprache des Entwicklers.
    var texts = WithCulture("it", () => new[]
    {
      PresenceText.Playing,
      PresenceText.Paused,
      PresenceText.Advertisement,
      PresenceText.OpenOnYouTubeMusic
    });

    Assert.All(texts, t => Assert.DoesNotContain("ä", t));
    Assert.All(texts, t => Assert.DoesNotContain("ö", t));
    Assert.Equal("Advertisement", texts[2]);
  }
}
