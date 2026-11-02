using YTMPresence.Core;

namespace YTMPresence.Core.Tests;

public class SourceSelectorTests
{
  private static readonly DateTimeOffset T0 = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

  private static YtmState State(string title, bool isPlaying)
    => new(
      Source: "ytmusic",
      Title: title,
      Artist: "Artist",
      Album: null,
      AlbumArtUrl: null,
      IsPlaying: isPlaying,
      PositionSeconds: null,
      DurationSeconds: null,
      Url: "https://music.youtube.com/watch?v=abc123",
      ShareUrl: null,
      IsAd: false,
      TimestampUnixMs: 0,
      Token: null);

  [Fact]
  public void FirstSource_IsSelected()
  {
    var selector = new SourceSelector();

    var current = selector.Update(1, "Chrome #1", State("A", isPlaying: false), T0);

    Assert.Equal("A", current?.Title);
    Assert.Equal(1, selector.SelectedId);
    Assert.Equal("Chrome #1", selector.SelectedLabel);
  }

  [Fact]
  public void PlayingSelection_IsKept_WhenAnotherSourceStartsPlaying()
  {
    var selector = new SourceSelector();
    selector.Update(1, "Chrome #1", State("A", isPlaying: true), T0);

    var current = selector.Update(2, "Edge #2", State("B", isPlaying: true), T0.AddSeconds(1));

    Assert.Null(current);
    Assert.Equal(1, selector.SelectedId);
  }

  [Fact]
  public void PausedSource_DoesNotReplaceThePlayingSelection()
  {
    // Genau das passierte bisher mit zwei Browsern: Der pausierte zweite
    // ueberschrieb die Presence des spielenden ersten.
    var selector = new SourceSelector();
    selector.Update(1, "Chrome #1", State("A", isPlaying: true), T0);

    var current = selector.Update(2, "Edge #2", State("B", isPlaying: false), T0.AddSeconds(1));

    Assert.Null(current);
    Assert.Equal(1, selector.SelectedId);
  }

  [Fact]
  public void PlayingSource_TakesOver_WhenTheSelectionIsPaused()
  {
    var selector = new SourceSelector();
    selector.Update(1, "Chrome #1", State("A", isPlaying: false), T0);

    var current = selector.Update(2, "Edge #2", State("B", isPlaying: true), T0.AddSeconds(1));

    Assert.Equal("B", current?.Title);
    Assert.Equal(2, selector.SelectedId);
  }

  [Fact]
  public void PausingTheSelection_SwitchesToAnotherPlayingSource()
  {
    var selector = new SourceSelector();
    selector.Update(1, "Chrome #1", State("A", isPlaying: true), T0);
    selector.Update(2, "Edge #2", State("B", isPlaying: true), T0.AddSeconds(1));

    var current = selector.Update(1, "Chrome #1", State("A", isPlaying: false), T0.AddSeconds(2));

    Assert.Equal("B", current?.Title);
    Assert.Equal(2, selector.SelectedId);
  }

  [Fact]
  public void StalePlayingState_DoesNotTakeOver()
  {
    // Eine spielende Quelle meldet sich etwa jede Sekunde. Ein "spielt"
    // von vor 20 Sekunden ist kein Grund, die Auswahl zu wechseln.
    var selector = new SourceSelector();
    selector.Update(1, "Chrome #1", State("A", isPlaying: true), T0);
    selector.Update(2, "Edge #2", State("B", isPlaying: true), T0);

    var current = selector.Update(1, "Chrome #1", State("A", isPlaying: false), T0.AddSeconds(20));

    Assert.Equal("A", current?.Title);
    Assert.Equal(1, selector.SelectedId);
  }

  [Fact]
  public void WithoutPlayingSources_TheSelectionIsKept()
  {
    var selector = new SourceSelector();
    selector.Update(1, "Chrome #1", State("A", isPlaying: false), T0);

    var current = selector.Update(2, "Edge #2", State("B", isPlaying: false), T0.AddSeconds(1));

    Assert.Null(current);
    Assert.Equal(1, selector.SelectedId);
  }

  [Fact]
  public void SelectedSource_StaysCurrent_ForItsOwnUpdates()
  {
    var selector = new SourceSelector();
    selector.Update(1, "Chrome #1", State("A", isPlaying: true), T0);

    var current = selector.Update(1, "Chrome #1", State("A2", isPlaying: true), T0.AddSeconds(1));

    Assert.Equal("A2", current?.Title);
    Assert.Equal(1, selector.SelectedId);
  }

  [Fact]
  public void RemovingTheSelection_PrefersAPlayingSource()
  {
    var selector = new SourceSelector();
    selector.Update(1, "Chrome #1", State("A", isPlaying: true), T0);
    selector.Update(2, "Edge #2", State("B", isPlaying: false), T0.AddSeconds(1));
    selector.Update(3, "Firefox #3", State("C", isPlaying: true), T0.AddSeconds(2));

    var current = selector.Remove(1, T0.AddSeconds(3));

    Assert.Equal("C", current?.Title);
    Assert.Equal(3, selector.SelectedId);
  }

  [Fact]
  public void RemovingTheSelection_WithoutPlayingSources_PicksTheMostRecentlyPlayed()
  {
    var selector = new SourceSelector();
    selector.Update(1, "Chrome #1", State("A", isPlaying: true), T0);
    selector.Update(2, "Edge #2", State("B", isPlaying: true), T0.AddSeconds(1));
    selector.Update(2, "Edge #2", State("B", isPlaying: false), T0.AddSeconds(2));
    selector.Update(3, "Firefox #3", State("C", isPlaying: false), T0.AddSeconds(3));

    var current = selector.Remove(1, T0.AddSeconds(4));

    Assert.Equal("B", current?.Title);
    Assert.Equal(2, selector.SelectedId);
  }

  [Fact]
  public void RemovingAnotherSource_KeepsTheSelection()
  {
    var selector = new SourceSelector();
    selector.Update(1, "Chrome #1", State("A", isPlaying: true), T0);
    selector.Update(2, "Edge #2", State("B", isPlaying: false), T0.AddSeconds(1));

    var current = selector.Remove(2, T0.AddSeconds(2));

    Assert.Null(current);
    Assert.Equal(1, selector.SelectedId);
  }

  [Fact]
  public void RemovingTheLastSource_ClearsTheSelection()
  {
    var selector = new SourceSelector();
    selector.Update(1, "Chrome #1", State("A", isPlaying: true), T0);

    var current = selector.Remove(1, T0.AddSeconds(1));

    Assert.Null(current);
    Assert.Null(selector.SelectedId);
    Assert.Null(selector.SelectedLabel);
  }

  [Fact]
  public void NewSource_IsSelected_AfterTheLastOneWasRemoved()
  {
    var selector = new SourceSelector();
    selector.Update(1, "Chrome #1", State("A", isPlaying: true), T0);
    selector.Remove(1, T0.AddSeconds(1));

    var current = selector.Update(2, "Edge #2", State("B", isPlaying: false), T0.AddSeconds(2));

    Assert.Equal("B", current?.Title);
    Assert.Equal(2, selector.SelectedId);
  }

  [Theory]
  [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/155.0.0.0 Safari/537.36 Edg/155.0.4283.45", "Edge 155 #3")]
  [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/155.0.0.0 Safari/537.36", "Chrome 155 #3")]
  [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:140.0) Gecko/20100101 Firefox/140.0", "Firefox 140 #3")]
  [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/155.0.0.0 Safari/537.36 OPR/120.0.0.0", "Opera 120 #3")]
  [InlineData("", "Unknown browser #3")]
  [InlineData(null, "Unknown browser #3")]
  public void DescribeSource_NamesTheBrowser(string? userAgent, string expected)
  {
    Assert.Equal(expected, SourceSelector.DescribeSource(userAgent, 3));
  }
}
