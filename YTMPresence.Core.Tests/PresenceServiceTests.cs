using YTMPresence.Core;

namespace YTMPresence.Core.Tests;

public class PresenceServiceTests
{
  private sealed class FakeDiscordClient : IDiscordPresenceClient
  {
    public int EnsureCount;
    public int SetCount;
    public int ClearCount;
    public DiscordActivity? LastActivity;

    // Was Discord auf den naechsten Befehl antwortet.
    public DiscordCommandResult NextResult = DiscordCommandResult.Accepted;

    public Task EnsureConnectedAsync(CancellationToken ct)
    {
      EnsureCount++;
      return Task.CompletedTask;
    }

    public Task<DiscordCommandResult> SetActivityAsync(DiscordActivity activity, CancellationToken ct)
    {
      SetCount++;
      LastActivity = activity;
      return Task.FromResult(NextResult);
    }

    public Task<DiscordCommandResult> ClearActivityAsync(CancellationToken ct)
    {
      ClearCount++;
      return Task.FromResult(NextResult);
    }
  }

  private sealed class ConcurrencyDetectingDiscordClient : IDiscordPresenceClient
  {
    private int _activeOperations;
    private int _maxConcurrentOperations;

    public int MaxConcurrentOperations => Volatile.Read(ref _maxConcurrentOperations);

    public Task EnsureConnectedAsync(CancellationToken ct) => Task.CompletedTask;

    public Task<DiscordCommandResult> SetActivityAsync(DiscordActivity activity, CancellationToken ct)
      => SimulateOperationAsync(ct);

    public Task<DiscordCommandResult> ClearActivityAsync(CancellationToken ct)
      => SimulateOperationAsync(ct);

    private async Task<DiscordCommandResult> SimulateOperationAsync(CancellationToken ct)
    {
      var active = Interlocked.Increment(ref _activeOperations);
      InterlockedExtensions.Max(ref _maxConcurrentOperations, active);

      try
      {
        await Task.Delay(25, ct);
        return DiscordCommandResult.Accepted;
      }
      finally
      {
        Interlocked.Decrement(ref _activeOperations);
      }
    }
  }

  private static YtmState Playing(string title = "Song", string artist = "Artist", bool isPlaying = true, bool isAd = false)
    => new(
      Source: "ytmusic",
      Title: title,
      Artist: artist,
      Album: null,
      AlbumArtUrl: null,
      IsPlaying: isPlaying,
      PositionSeconds: null,
      DurationSeconds: null,
      Url: "https://music.youtube.com/watch?v=abc123",
      ShareUrl: null,
      IsAd: isAd,
      TimestampUnixMs: 0,
      Token: null);

  [Fact]
  public async Task PlayingTrack_SetsActivity()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing(title: "Hello"), CancellationToken.None);

    Assert.Equal(1, discord.SetCount);
    Assert.Equal("Hello", discord.LastActivity?.Details);
  }

  [Fact]
  public async Task NonYtmusicSource_IsIgnored()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    var foreign = Playing() with { Source = "spotify" };
    await service.HandleAsync(foreign, CancellationToken.None);

    Assert.Equal(0, discord.SetCount);
    Assert.Equal(0, discord.ClearCount);
  }

  [Fact]
  public async Task OnlyShowWhenPlaying_ClearsWhenPaused()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings { OnlyShowWhenPlaying = true });

    await service.HandleAsync(Playing(isPlaying: false), CancellationToken.None);

    Assert.Equal(0, discord.SetCount);
    Assert.True(discord.ClearCount >= 1);
  }

  [Fact]
  public async Task TrackChange_UpdatesWithoutClearingFirst()
  {
    // Frueher wurde vor jedem kritischen Update ein CLEAR gesendet und 200ms
    // gewartet ("Discord Cache Fix"). Das blieb folgenlos, solange das Loeschen
    // den nicht existierenden Befehl CLEAR_ACTIVITY benutzte. Seit das Loeschen
    // wirkt, wuerde derselbe Ablauf die Presence bei jedem Songwechsel kurz
    // verschwinden lassen. SET_ACTIVITY ersetzt die Aktivitaet von sich aus.
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing(title: "Erster Song"), CancellationToken.None);
    await service.HandleAsync(Playing(title: "Zweiter Song"), CancellationToken.None);

    Assert.Equal(2, discord.SetCount);
    Assert.Equal(0, discord.ClearCount);
  }

  [Fact]
  public async Task PlayPauseToggle_UpdatesWithoutClearingFirst()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing(), CancellationToken.None);
    await service.HandleAsync(Playing(isPlaying: false), CancellationToken.None);

    Assert.Equal(2, discord.SetCount);
    Assert.Equal(0, discord.ClearCount);
  }

  [Theory]
  [InlineData("https://music.youtube.com/watch?v=abc123")]
  [InlineData("https://music.youtube.com/search?q=Sam%20Smith")]
  public void ValidButtonUrls_AreAccepted(string url)
  {
    Assert.True(PresenceService.IsValidButtonUrl(url));
  }

  [Theory]
  // Genau dieser Fall hat Discord die komplette Aktivitaet ablehnen lassen:
  // Uri.ToString() loest die Prozent-Kodierung auf, aus %20 wird ein echtes
  // Leerzeichen. Uri.TryCreate akzeptiert das anstandslos, Discord nicht.
  [InlineData("https://music.youtube.com/search?q=Sam Smith")]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("nicht mal eine url")]
  [InlineData("javascript:alert(1)")]
  [InlineData("ftp://example.com/track")]
  public void InvalidButtonUrls_AreRejected(string url)
  {
    Assert.False(PresenceService.IsValidButtonUrl(url));
  }

  [Fact]
  public async Task PercentEncodedTrackUrl_StaysEncodedInTheButton()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    var state = Playing() with { ShareUrl = "https://music.youtube.com/search?q=Sam%20Smith" };
    await service.HandleAsync(state, CancellationToken.None);

    var buttons = discord.LastActivity?.Buttons;
    Assert.NotNull(buttons);
    Assert.All(buttons!, b => Assert.True(
        PresenceService.IsValidButtonUrl(b.Url),
        $"Button-URL '{b.Url}' wuerde von Discord abgelehnt."));
  }

  [Fact]
  public async Task InvalidTrackUrl_DropsOnlyThatButton()
  {
    // Ein fragwuerdiger Link darf hoechstens sich selbst kosten. Frueher hat er
    // die gesamte Presence verhindert, weil Discord die Aktivitaet ablehnte.
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    var state = Playing() with { Url = "nicht mal eine url", ShareUrl = "auch keine" };
    await service.HandleAsync(state, CancellationToken.None);

    Assert.Equal(1, discord.SetCount);

    var buttons = discord.LastActivity?.Buttons;
    Assert.NotNull(buttons);
    Assert.NotEmpty(buttons!);
    Assert.All(buttons!, b => Assert.True(PresenceService.IsValidButtonUrl(b.Url)));
  }

  [Fact]
  public async Task IdenticalState_IsDeduped()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    var state = Playing();
    await service.HandleAsync(state, CancellationToken.None);
    await service.HandleAsync(state, CancellationToken.None);

    // Zweite identische Nachricht wird durch Min-Intervall + Key-Dedupe verworfen.
    Assert.Equal(1, discord.SetCount);
  }

  [Fact]
  public async Task Ad_Ignore_DoesNothing()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings { AdBehavior = AdBehavior.Ignore });

    await service.HandleAsync(Playing(isAd: true), CancellationToken.None);

    Assert.Equal(0, discord.SetCount);
    Assert.Equal(0, discord.ClearCount);
  }

  [Fact]
  public async Task Ad_Clear_ClearsActivity()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings { AdBehavior = AdBehavior.Clear });

    await service.HandleAsync(Playing(isAd: true), CancellationToken.None);

    Assert.True(discord.ClearCount >= 1);
    Assert.Equal(0, discord.SetCount);
  }

  [Fact]
  public async Task Ad_Show_SetsAdActivity()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings { AdBehavior = AdBehavior.ShowAdvertisement });

    await service.HandleAsync(Playing(isAd: true), CancellationToken.None);

    Assert.Equal(1, discord.SetCount);

    // Nicht gegen "Werbung" pruefen: Der Text folgt seit der Lokalisierung der
    // Systemsprache, und der CI-Runner laeuft auf Englisch. Hier gehoert die
    // Verdrahtung geprueft - dass der Werbezustand den Werbetext setzt. Die
    // Uebersetzungen selbst deckt PresenceTextTests ab.
    Assert.Equal(PresenceText.Advertisement, discord.LastActivity?.State);
    Assert.Equal("YouTube Music", discord.LastActivity?.Details);
  }

  [Fact]
  public async Task AcceptedActivity_ReportsDiscordOk()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing(), CancellationToken.None);

    var status = service.GetStatusSnapshot();
    Assert.True(status.DiscordOk);
    Assert.Null(status.LastDiscordError);
    Assert.NotNull(status.LastDiscordOkUtc);
    Assert.Equal(DiscordCommandOutcome.Accepted, status.LastDiscordOutcome);
  }

  [Fact]
  public async Task RejectedActivity_IsReportedWithDiscordsReason()
  {
    // Frueher galt die Presence als gesetzt, sobald der Befehl geschrieben war.
    // Eine Ablehnung stand nur im Log, die Diagnose meldete weiter OK.
    var discord = new FakeDiscordClient
    {
      NextResult = DiscordCommandResult.Rejected(4000, "child \"activity\" fails")
    };
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing(), CancellationToken.None);

    var status = service.GetStatusSnapshot();
    Assert.False(status.DiscordOk);
    Assert.Equal(DiscordCommandOutcome.Rejected, status.LastDiscordOutcome);
    Assert.Contains("child \"activity\" fails", status.LastDiscordError);
    Assert.Contains("4000", status.LastDiscordError);
    Assert.Null(status.LastDiscordOkUtc);
  }

  [Fact]
  public async Task RejectedActivity_IsNotResentForTheSameState()
  {
    // Dieselbe Aktivitaet wuerde nur erneut abgelehnt.
    var discord = new FakeDiscordClient
    {
      NextResult = DiscordCommandResult.Rejected(4000, "invalid")
    };
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing(), CancellationToken.None);
    await service.HandleAsync(Playing(), CancellationToken.None);

    Assert.Equal(1, discord.SetCount);
  }

  [Fact]
  public async Task UnansweredActivity_IsReportedAsNoReply()
  {
    var discord = new FakeDiscordClient { NextResult = DiscordCommandResult.NoReply };
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing(), CancellationToken.None);

    var status = service.GetStatusSnapshot();
    Assert.False(status.DiscordOk);
    Assert.Equal(DiscordCommandOutcome.NoReply, status.LastDiscordOutcome);
    Assert.False(string.IsNullOrWhiteSpace(status.LastDiscordError));
  }

  [Fact]
  public async Task AcceptedActivity_AfterRejection_RestoresDiscordOk()
  {
    var discord = new FakeDiscordClient
    {
      NextResult = DiscordCommandResult.Rejected(4000, "invalid")
    };
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing(title: "First"), CancellationToken.None);
    discord.NextResult = DiscordCommandResult.Accepted;
    await service.HandleAsync(Playing(title: "Second"), CancellationToken.None);

    var status = service.GetStatusSnapshot();
    Assert.True(status.DiscordOk);
    Assert.Null(status.LastDiscordError);
    Assert.Equal(DiscordCommandOutcome.Accepted, status.LastDiscordOutcome);
  }

  [Fact]
  public async Task RejectedClear_IsReported()
  {
    var discord = new FakeDiscordClient
    {
      NextResult = DiscordCommandResult.Rejected(4000, "cannot clear")
    };
    var service = new PresenceService(discord, new AppSettings { OnlyShowWhenPlaying = true });

    await service.HandleAsync(Playing(isPlaying: false), CancellationToken.None);

    var status = service.GetStatusSnapshot();
    Assert.True(discord.ClearCount >= 1);
    Assert.False(status.DiscordOk);
    Assert.Equal(DiscordCommandOutcome.Rejected, status.LastDiscordOutcome);
    Assert.Contains("cannot clear", status.LastDiscordError);
  }

  [Fact]
  public async Task ConnectionError_ClearsTheLastOutcome()
  {
    var discord = new ThrowingDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing(), CancellationToken.None);

    var status = service.GetStatusSnapshot();
    Assert.False(status.DiscordOk);
    Assert.Null(status.LastDiscordOutcome);
    Assert.Equal("not reachable", status.LastDiscordError);
  }

  private sealed class ThrowingDiscordClient : IDiscordPresenceClient
  {
    public Task EnsureConnectedAsync(CancellationToken ct)
      => throw new InvalidOperationException("not reachable");

    public Task<DiscordCommandResult> SetActivityAsync(DiscordActivity activity, CancellationToken ct)
      => throw new InvalidOperationException("not reachable");

    public Task<DiscordCommandResult> ClearActivityAsync(CancellationToken ct)
      => throw new InvalidOperationException("not reachable");
  }

  [Fact]
  public async Task ConcurrentStates_AreSerialized()
  {
    var discord = new ConcurrencyDetectingDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    await Task.WhenAll(
      service.HandleAsync(Playing(title: "First"), CancellationToken.None),
      service.HandleAsync(Playing(title: "Second"), CancellationToken.None));

    Assert.Equal(1, discord.MaxConcurrentOperations);
  }
}

internal static class InterlockedExtensions
{
  public static void Max(ref int location, int value)
  {
    var current = Volatile.Read(ref location);
    while (current < value)
    {
      var observed = Interlocked.CompareExchange(ref location, value, current);
      if (observed == current)
        return;

      current = observed;
    }
  }
}
