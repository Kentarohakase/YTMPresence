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

    // Simuliert einen Verbindungsverlust, etwa einen Neustart von Discord.
    public bool IsReady { get; set; } = true;

    public Task EnsureConnectedAsync(CancellationToken ct)
    {
      EnsureCount++;
      IsReady = true;
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

    public bool IsReady => true;

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

  private const string CoverUrl = "https://lh3.googleusercontent.com/abcDEF123-xyz=w544-h544-l90-rj";

  [Fact]
  public async Task AlbumArt_IsUsedAsLargeImage()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing() with { AlbumArtUrl = CoverUrl }, CancellationToken.None);

    Assert.Equal(CoverUrl, discord.LastActivity?.Assets?.LargeImage);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task AlbumArt_HasNoPlayPauseIconOnTop(bool isPlaying)
  {
    // Das Symbol würde einen Teil des Covers verdecken. Der Status steht im
    // Statustext.
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing(isPlaying: isPlaying) with { AlbumArtUrl = CoverUrl }, CancellationToken.None);

    Assert.Null(discord.LastActivity?.Assets?.SmallImage);
    Assert.Null(discord.LastActivity?.Assets?.SmallText);
  }

  [Theory]
  [InlineData(true, "play")]
  [InlineData(false, "pause")]
  public async Task DefaultImage_KeepsPlayPauseIcon(bool isPlaying, string expectedIcon)
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing(isPlaying: isPlaying), CancellationToken.None);

    Assert.Equal("ytm_large", discord.LastActivity?.Assets?.LargeImage);
    Assert.Equal(expectedIcon, discord.LastActivity?.Assets?.SmallImage);
  }

  [Fact]
  public async Task MusicVideoThumbnail_IsSentWithoutLetterbox()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    var state = Playing() with
    {
      AlbumArtUrl = "https://i.ytimg.com/vi/abcdefghijk/sddefault.jpg?sqp=-oaymwEWCJADEOEBIAQqCghqEJQEGHgg6AJIWg&rs=AMzJL3k"
    };
    await service.HandleAsync(state, CancellationToken.None);

    Assert.Equal("https://i.ytimg.com/vi/abcdefghijk/mqdefault.jpg", discord.LastActivity?.Assets?.LargeImage);
  }

  [Theory]
  [InlineData("https://i.ytimg.com/vi/abcdefghijk/default.jpg")]
  [InlineData("https://i.ytimg.com/vi/abcdefghijk/hqdefault.jpg")]
  [InlineData("https://i.ytimg.com/vi/abcdefghijk/sddefault.jpg?sqp=abc&rs=def")]
  [InlineData("https://i.ytimg.com/vi_webp/abcdefghijk/hqdefault.webp")]
  [InlineData("https://I.YTIMG.COM/vi/abcdefghijk/hqdefault.jpg")]
  public void LetterboxedThumbnails_UseTheWidescreenVariant(string url)
  {
    Assert.Equal(
        "https://i.ytimg.com/vi/abcdefghijk/mqdefault.jpg",
        PresenceService.PreferThumbnailWithoutLetterbox(url));
  }

  [Theory]
  // Album-Cover sind bereits quadratisch.
  [InlineData(CoverUrl)]
  // Schon 16:9 ohne Balken.
  [InlineData("https://i.ytimg.com/vi/abcdefghijk/mqdefault.jpg")]
  [InlineData("https://i.ytimg.com/vi/abcdefghijk/maxresdefault.jpg")]
  [InlineData("https://i.ytimg.com/vi/abcdefghijk/hq720.jpg?sqp=abc")]
  // Andere Hosts tragen eigene Signaturen; ein Umschreiben könnte sie brechen.
  [InlineData("https://i9.ytimg.com/vi/abcdefghijk/hqdefault.jpg?sqp=abc")]
  // Keine gültige Video-ID oder unbekannter Pfad.
  [InlineData("https://i.ytimg.com/vi/short/hqdefault.jpg")]
  [InlineData("https://i.ytimg.com/vi/abc%2Fdefghij/hqdefault.jpg")]
  [InlineData("https://i.ytimg.com/an_webp/abcdefghijk/mqdefault_6s.webp")]
  public void OtherImages_AreLeftUnchanged(string url)
  {
    Assert.Equal(url, PresenceService.PreferThumbnailWithoutLetterbox(url));
  }

  [Fact]
  public async Task AlbumArt_Disabled_UsesDefaultImage()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings { ShowAlbumArtInDiscord = false });

    await service.HandleAsync(Playing() with { AlbumArtUrl = CoverUrl }, CancellationToken.None);

    Assert.Equal("ytm_large", discord.LastActivity?.Assets?.LargeImage);
    Assert.Equal("play", discord.LastActivity?.Assets?.SmallImage);
  }

  [Fact]
  public async Task AlbumArt_Missing_UsesDefaultImage()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing(), CancellationToken.None);

    Assert.Equal("ytm_large", discord.LastActivity?.Assets?.LargeImage);
  }

  [Fact]
  public async Task AlbumArt_Unusable_FallsBackWithoutLosingThePresence()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(
        Playing() with { AlbumArtUrl = "https://example.com/cover.jpg" },
        CancellationToken.None);

    Assert.Equal(1, discord.SetCount);
    Assert.Equal("ytm_large", discord.LastActivity?.Assets?.LargeImage);
  }

  [Fact]
  public async Task AlbumArt_ArrivingLater_UpdatesThePresence()
  {
    // YouTube Music kann das Artwork nach dem Titel setzen. Ohne das Bild im
    // Presence-Key bliebe das Logo bis zum nächsten Fortschrittsschritt stehen.
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings { MinDiscordUpdateSeconds = 0 });

    await service.HandleAsync(Playing(), CancellationToken.None);
    await service.HandleAsync(Playing() with { AlbumArtUrl = CoverUrl }, CancellationToken.None);

    Assert.Equal(2, discord.SetCount);
    Assert.Equal(CoverUrl, discord.LastActivity?.Assets?.LargeImage);
  }

  [Fact]
  public async Task Ad_KeepsDefaultImageEvenWithAlbumArt()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings { AdBehavior = AdBehavior.ShowAdvertisement });

    await service.HandleAsync(Playing(isAd: true) with { AlbumArtUrl = CoverUrl }, CancellationToken.None);

    Assert.Equal("ytm_large", discord.LastActivity?.Assets?.LargeImage);
  }

  [Theory]
  [InlineData(CoverUrl)]
  [InlineData("https://lh6.googleusercontent.com/abc=w60-h60")]
  [InlineData("https://yt3.googleusercontent.com/abc=s544")]
  [InlineData("https://i.ytimg.com/vi/abcdefghijk/hqdefault.jpg")]
  [InlineData("https://i9.ytimg.com/vi/abcdefghijk/sddefault.jpg?sqp=abc")]
  [InlineData("https://yt3.ggpht.com/abc=s176")]
  public void AlbumArtUrls_FromYouTubeImageHosts_AreAccepted(string url)
  {
    Assert.True(PresenceService.IsDiscordAlbumArtUrl(url));
  }

  [Theory]
  [InlineData("")]
  [InlineData("http://lh3.googleusercontent.com/abc")]
  [InlineData("https://example.com/cover.jpg")]
  [InlineData("https://evilytimg.com/cover.jpg")]
  [InlineData("https://ytimg.com.example.com/cover.jpg")]
  // googleusercontent.com hostet auch beliebige Nutzerdateien.
  [InlineData("https://doc-0s-docs.googleusercontent.com/file")]
  [InlineData("https://lh3.googleusercontent.com:8443/abc")]
  [InlineData("https://user@lh3.googleusercontent.com/abc")]
  [InlineData("https://lh3.googleusercontent.com/a b")]
  public void AlbumArtUrls_OutsideTheAllowlist_AreRejected(string url)
  {
    Assert.False(PresenceService.IsDiscordAlbumArtUrl(url));
  }

  [Theory]
  // Über Discords Limit von 300 Zeichen.
  [InlineData(301)]
  // Ältere Erweiterungen kürzen URLs auf genau 256 Zeichen.
  [InlineData(256)]
  public void AlbumArtUrls_WithUnusableLength_AreRejected(int length)
  {
    const string prefix = "https://lh3.googleusercontent.com/";
    var url = prefix + new string('a', length - prefix.Length);

    Assert.False(PresenceService.IsDiscordAlbumArtUrl(url));
  }

  [Theory]
  [InlineData(255)]
  [InlineData(300)]
  public void AlbumArtUrls_WithinTheLimit_AreAccepted(int length)
  {
    const string prefix = "https://lh3.googleusercontent.com/";
    var url = prefix + new string('a', length - prefix.Length);

    Assert.True(PresenceService.IsDiscordAlbumArtUrl(url));
  }

  private static YtmState PlayingAt(double position, bool isPlaying = true)
    => Playing(isPlaying: isPlaying) with { Album = "Album", PositionSeconds = position, DurationSeconds = 200 };

  [Fact]
  public async Task PlayingTrack_LeavesTheTimeToDiscordsProgressBar()
  {
    // Discord zeigt aus den Zeitstempeln einen eigenen Balken mit Zeiten. Der
    // fruehere Text "Artist · 0:10 / 3:20" stand doppelt daneben.
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(PlayingAt(10), CancellationToken.None);

    Assert.Equal("Artist · Album", discord.LastActivity?.State);
    var timestamps = discord.LastActivity?.Timestamps;
    Assert.NotNull(timestamps);
    Assert.Equal(200, timestamps!.EndUnixSeconds - timestamps.StartUnixSeconds);
  }

  [Fact]
  public async Task NormalProgress_DoesNotResend()
  {
    // Frueher erzwang der Zeittext etwa alle 15 Sekunden ein Update. Kleine
    // Abweichungen der gemeldeten Position duerfen keins ausloesen.
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings { MinDiscordUpdateSeconds = 0 });

    await service.HandleAsync(PlayingAt(10), CancellationToken.None);
    await service.HandleAsync(PlayingAt(11), CancellationToken.None);
    await service.HandleAsync(PlayingAt(9), CancellationToken.None);

    Assert.Equal(1, discord.SetCount);
  }

  [Fact]
  public async Task Seek_SendsNewTimestamps()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings { MinDiscordUpdateSeconds = 0 });

    await service.HandleAsync(PlayingAt(10), CancellationToken.None);
    var before = discord.LastActivity!.Timestamps!.StartUnixSeconds!.Value;

    await service.HandleAsync(PlayingAt(100), CancellationToken.None);
    var after = discord.LastActivity!.Timestamps!.StartUnixSeconds!.Value;

    Assert.Equal(2, discord.SetCount);
    Assert.InRange(before - after, 89, 91);
  }

  [Fact]
  public async Task SeekWhilePaused_UpdatesThePausedPosition()
  {
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings { MinDiscordUpdateSeconds = 0 });

    await service.HandleAsync(PlayingAt(10, isPlaying: false), CancellationToken.None);
    await service.HandleAsync(PlayingAt(50, isPlaying: false), CancellationToken.None);

    Assert.Equal(2, discord.SetCount);
    Assert.Equal(PresenceText.PausedAt("0:50", "Artist"), discord.LastActivity?.State);
    Assert.Null(discord.LastActivity?.Timestamps);
  }

  [Fact]
  public async Task LostDiscordConnection_ResendsTheUnchangedPresence()
  {
    // Nach einem Neustart von Discord ist die Aktivitaet dort weg. Ohne den
    // frueheren Zeittext aendert sich der Key nicht mehr von selbst.
    var discord = new FakeDiscordClient();
    var service = new PresenceService(discord, new AppSettings { MinDiscordUpdateSeconds = 0 });

    await service.HandleAsync(PlayingAt(10), CancellationToken.None);
    discord.IsReady = false;
    await service.HandleAsync(PlayingAt(10), CancellationToken.None);

    Assert.Equal(2, discord.SetCount);
    Assert.True(discord.IsReady);
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

  [Fact]
  public async Task DiscordNotRunning_IsReportedAsSuch()
  {
    // Die Oberflaeche zeigt dann einen klaren Hinweis statt der englischen
    // Fehlermeldung des IPC-Clients.
    var discord = new ThrowingDiscordClient(new DiscordUnavailableException("Discord IPC is not reachable."));
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing(), CancellationToken.None);

    var status = service.GetStatusSnapshot();
    Assert.False(status.DiscordOk);
    Assert.True(status.DiscordNotRunning);
  }

  [Fact]
  public async Task OtherConnectionErrors_AreNotReportedAsDiscordNotRunning()
  {
    var discord = new ThrowingDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing(), CancellationToken.None);

    Assert.False(service.GetStatusSnapshot().DiscordNotRunning);
  }

  [Fact]
  public async Task DiscordNotRunning_IsClearedOnceDiscordAnswers()
  {
    var discord = new SwitchableDiscordClient();
    var service = new PresenceService(discord, new AppSettings());

    await service.HandleAsync(Playing(title: "First"), CancellationToken.None);
    Assert.True(service.GetStatusSnapshot().DiscordNotRunning);

    discord.Running = true;
    await service.HandleAsync(Playing(title: "Second"), CancellationToken.None);

    var status = service.GetStatusSnapshot();
    Assert.True(status.DiscordOk);
    Assert.False(status.DiscordNotRunning);
  }

  private sealed class SwitchableDiscordClient : IDiscordPresenceClient
  {
    public bool Running;

    public bool IsReady => Running;

    public Task EnsureConnectedAsync(CancellationToken ct)
      => Running ? Task.CompletedTask : throw new DiscordUnavailableException("not running");

    public Task<DiscordCommandResult> SetActivityAsync(DiscordActivity activity, CancellationToken ct)
      => Task.FromResult(DiscordCommandResult.Accepted);

    public Task<DiscordCommandResult> ClearActivityAsync(CancellationToken ct)
      => Task.FromResult(DiscordCommandResult.Accepted);
  }

  private sealed class ThrowingDiscordClient(Exception? error = null) : IDiscordPresenceClient
  {
    private readonly Exception _error = error ?? new InvalidOperationException("not reachable");

    public bool IsReady => false;

    public Task EnsureConnectedAsync(CancellationToken ct)
      => throw _error;

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
