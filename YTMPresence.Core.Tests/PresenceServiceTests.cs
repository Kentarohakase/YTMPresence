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

    public Task EnsureConnectedAsync(CancellationToken ct)
    {
      EnsureCount++;
      return Task.CompletedTask;
    }

    public Task SetActivityAsync(DiscordActivity activity, CancellationToken ct)
    {
      SetCount++;
      LastActivity = activity;
      return Task.CompletedTask;
    }

    public Task ClearActivityAsync(CancellationToken ct)
    {
      ClearCount++;
      return Task.CompletedTask;
    }
  }

  private sealed class ConcurrencyDetectingDiscordClient : IDiscordPresenceClient
  {
    private int _activeOperations;
    private int _maxConcurrentOperations;

    public int MaxConcurrentOperations => Volatile.Read(ref _maxConcurrentOperations);

    public Task EnsureConnectedAsync(CancellationToken ct) => Task.CompletedTask;

    public Task SetActivityAsync(DiscordActivity activity, CancellationToken ct)
      => SimulateOperationAsync(ct);

    public Task ClearActivityAsync(CancellationToken ct)
      => SimulateOperationAsync(ct);

    private async Task SimulateOperationAsync(CancellationToken ct)
    {
      var active = Interlocked.Increment(ref _activeOperations);
      InterlockedExtensions.Max(ref _maxConcurrentOperations, active);

      try
      {
        await Task.Delay(25, ct);
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
    Assert.Equal("Werbung", discord.LastActivity?.State);
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
