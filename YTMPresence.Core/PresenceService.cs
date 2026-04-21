namespace YTMPresence.Core;

public sealed class PresenceService
{
  private readonly DiscordIpcClient _discord;
  private readonly AppSettings _settings;

  private string? _lastPresenceKey;
  private DateTimeOffset _lastUpdateUtc = DateTimeOffset.MinValue;
  private DateTimeOffset _lastIncomingUtc = DateTimeOffset.MinValue;

  // Discord Cache Fix (für Play/Pause/Trackwechsel)
  private bool _forceClearBeforeNextSet = true;

  // Track/PlayState Change Detection
  private string? _lastSongKey;
  private bool? _lastIsPlaying;
  private DateTimeOffset? _pausedSinceUtc;

  // Wie oft soll der "Zeit-Text" aktualisiert werden?
  // Beispiel: 10 => "01:20 / 03:45" wird nur alle 10 Sekunden neu gesetzt.
  private const int ProgressTextStepSeconds = 10;

  public bool DiscordOk { get; private set; } = true;
  public string? LastDiscordError { get; private set; }
  public DateTimeOffset? LastDiscordOkUtc { get; private set; }

  public PresenceService(DiscordIpcClient discord, AppSettings settings)
  {
    _discord = discord;
    _settings = settings;
  }

  public async Task HandleAsync(YtmState state, CancellationToken ct)
  {
    var now = DateTimeOffset.UtcNow;
    _lastIncomingUtc = now;

    if (!string.Equals(state.Source, "ytmusic", StringComparison.Ordinal))
      return;

    if (_settings.OnlyShowWhenPlaying && !state.IsPlaying)
    {
      await ClearPresenceAsync(ct);
      ResetPresenceTracking();
      return;
    }

    var rawTitle = state.Title ?? "";
    var rawArtist = ResolveArtist(state);
    var songKey = state.IsAd ? "AD" : $"{Clamp(rawTitle, 128)}\u001F{Clamp(rawArtist, 128)}";
    var playStateChanged = _lastIsPlaying is null || _lastIsPlaying.Value != state.IsPlaying;
    var trackChanged = !string.Equals(songKey, _lastSongKey, StringComparison.Ordinal);

    _lastSongKey = songKey;
    _lastIsPlaying = state.IsPlaying;
    _pausedSinceUtc = state.IsPlaying ? null : (_pausedSinceUtc ?? now);

    var criticalChange = trackChanged || playStateChanged;
    if (criticalChange)
      _forceClearBeforeNextSet = true;

    if (state.IsAd)
    {
      _pausedSinceUtc = null;
      await HandleAdAsync(criticalChange, ct);
      return;
    }

    if (string.IsNullOrWhiteSpace(rawTitle))
      return;

    var title = Clamp(rawTitle, 128);
    var artist = Clamp(rawArtist, 128);

    var trackUrl = GetBestTrackUrl(state);

    // Progress-Bucket erzwingt Updates, damit die Zeit-Anzeige sich ändert
    // (ohne Bucket wäre "changed=false" und Discord würde nie neu gesetzt werden)
    var progressBucket = GetProgressBucket(state);

    var activity = BuildActivity(state, title, artist, trackUrl);

    // Key enthält Bucket => Update alle ProgressTextStepSeconds Sekunden
    var key = $"{state.IsPlaying}|{title}|{artist}|{trackUrl}|b{progressBucket}";

    await TryUpdateAsync(key, activity, force: criticalChange, ct);
  }

  public async Task ClearIfIdleAsync(CancellationToken ct)
  {
    if (_lastIncomingUtc == DateTimeOffset.MinValue)
      return;

    var now = DateTimeOffset.UtcNow;

    if (_pausedSinceUtc is not null)
    {
      var pausedSeconds = (now - _pausedSinceUtc.Value).TotalSeconds;
      if (pausedSeconds >= _settings.PausedClearSeconds)
      {
        await ClearPresenceAsync(ct);
        ResetPresenceTracking();
        return;
      }
    }

    var idleSeconds = (now - _lastIncomingUtc).TotalSeconds;
    if (idleSeconds < _settings.IdleClearSeconds)
      return;

    await ClearPresenceAsync(ct);
    ResetPresenceTracking();
  }

  private async Task ClearPresenceAsync(CancellationToken ct)
  {
    try
    {
      await _discord.EnsureConnectedAsync(ct);
      await _discord.ClearActivityAsync(ct);
      MarkDiscordOk();
    }
    catch (Exception ex)
    {
      MarkDiscordError(ex);
    }
  }

  private void ResetPresenceTracking()
  {
    _lastIncomingUtc = DateTimeOffset.MinValue;
    _lastPresenceKey = null;
    _lastUpdateUtc = DateTimeOffset.MinValue;

    _forceClearBeforeNextSet = true;
    _lastSongKey = null;
    _lastIsPlaying = null;
    _pausedSinceUtc = null;
  }

  private async Task HandleAdAsync(bool criticalChange, CancellationToken ct)
  {
    switch (_settings.AdBehavior)
    {
      case AdBehavior.Ignore:
        return;

      case AdBehavior.Clear:
        await ClearPresenceAsync(ct);
        ResetPresenceTracking();
        return;

      default:
        var adAssets = new DiscordAssets(
            LargeImage: _settings.Assets.LargeImageKey,
            LargeText: _settings.Assets.LargeImageText,
            SmallImage: null,
            SmallText: null);

        var adActivity = new DiscordActivity(
            Details: "YouTube Music",
            State: "Werbung",
            Timestamps: null,
            Assets: adAssets,
            Buttons: null,
            Type: DiscordActivityType.Listening
        );

        await TryUpdateAsync("AD", adActivity, force: criticalChange, ct);
        return;
    }
  }

  private async Task TryUpdateAsync(string key, DiscordActivity activity, bool force, CancellationToken ct)
  {
    var now = DateTimeOffset.UtcNow;
    var minInterval = TimeSpan.FromSeconds(_settings.MinDiscordUpdateSeconds);

    var changed = !string.Equals(key, _lastPresenceKey, StringComparison.Ordinal);

    // Critical changes (Track oder Play/Pause) sofort zulassen
    var allowed = force || (now - _lastUpdateUtc) >= minInterval;

    if (!changed || !allowed)
      return;

    try
    {
      await _discord.EnsureConnectedAsync(ct);

      // Discord Cache Fix: bei critical einmal CLEAR vor SET
      if (_forceClearBeforeNextSet)
      {
        _forceClearBeforeNextSet = false;
        await _discord.ClearActivityAsync(ct);
        await Task.Delay(200, ct);
      }

      await _discord.SetActivityAsync(activity, ct);

      _lastPresenceKey = key;
      _lastUpdateUtc = now;

      MarkDiscordOk();
    }
    catch (Exception ex)
    {
      MarkDiscordError(ex);
      _forceClearBeforeNextSet = true;
    }
  }

  private DiscordActivity BuildActivity(YtmState state, string title, string artist, string trackUrl)
  {
    // "Spotify-like": Balken ist Discord-seitig nicht garantiert.
    // Daher: Zeit als Text im State anzeigen.
    var hasTimes = TryGetTimes(state, out var pos, out var dur);
    var album = NormalizeAlbum(state.Album, title, artist);

    // Timestamps nur beim Playing (optional – Discord kann dann trotzdem irgendwo Zeit anzeigen)
    var timestamps = state.IsPlaying ? TryBuildTimestamps(state) : null;

    var assets = new DiscordAssets(
        LargeImage: _settings.Assets.LargeImageKey,
        LargeText: string.IsNullOrWhiteSpace(album)
            ? _settings.Assets.LargeImageText
            : $"{album} · {_settings.Assets.LargeImageText}",
        SmallImage: state.IsPlaying ? _settings.Assets.PlaySmallImageKey : _settings.Assets.PauseSmallImageKey,
        SmallText: state.IsPlaying ? "Läuft" : "Pausiert"
    );

    DiscordButton[] buttons =
    {
            new DiscordButton("Auf YouTube Music öffnen", trackUrl),
            new DiscordButton("YouTube Music", "https://music.youtube.com/")
        };

    // Zeit im Status: "Artist · 01:23 / 03:45" oder "Pausiert bei 01:23 · Artist"
    string stateLine;
    if (state.IsPlaying)
    {
      stateLine = hasTimes
          ? $"{artist} · {Fmt(pos)} / {Fmt(dur)}"
          : AppendAlbum(artist, album);
    }
    else
    {
      stateLine = hasTimes
          ? $"Pausiert bei {Fmt(pos)} · {artist}"
          : $"Pausiert · {AppendAlbum(artist, album)}";
    }

    return new DiscordActivity(
        Details: title,
        State: stateLine,
        Timestamps: timestamps,
        Assets: assets,
        Buttons: buttons,
        Type: DiscordActivityType.Listening
    );
  }

  private static bool TryGetTimes(YtmState state, out double pos, out double dur)
  {
    pos = 0;
    dur = 0;

    if (state.PositionSeconds is not double p || state.DurationSeconds is not double d)
      return false;

    if (!double.IsFinite(p) || !double.IsFinite(d))
      return false;

    if (d <= 0 || p < 0)
      return false;

    // clamp p to [0..d] (YT kann manchmal kurz drüber liegen)
    if (p > d) p = d;

    pos = p;
    dur = d;
    return true;
  }

  private static int GetProgressBucket(YtmState state)
  {
    if (TryGetTimes(state, out var pos, out _))
      return (int)(pos / ProgressTextStepSeconds);

    return -1;
  }

  private static string Fmt(double seconds)
  {
    if (!double.IsFinite(seconds) || seconds < 0) return "0:00";
    var ts = TimeSpan.FromSeconds(seconds);
    return ts.Hours > 0
        ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
        : $"{ts.Minutes}:{ts.Seconds:D2}";
  }

  private static DiscordTimestamps? TryBuildTimestamps(YtmState state)
  {
    if (!state.IsPlaying)
      return null;

    if (state.PositionSeconds is not double pos || state.DurationSeconds is not double dur)
      return null;

    if (!double.IsFinite(pos) || !double.IsFinite(dur))
      return null;

    if (dur <= 0 || pos < 0 || pos > dur)
      return null;

    var start = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(pos);
    var end = start + TimeSpan.FromSeconds(dur);

    return new DiscordTimestamps(start.ToUnixTimeSeconds(), end.ToUnixTimeSeconds());
  }

  private static string GetBestTrackUrl(YtmState state)
  {
    var candidate =
        !string.IsNullOrWhiteSpace(state.ShareUrl) ? state.ShareUrl!.Trim() :
        !string.IsNullOrWhiteSpace(state.Url) ? state.Url.Trim() :
        "";

    if (candidate.StartsWith("https://music.youtube.com/watch?v=", StringComparison.OrdinalIgnoreCase))
      return candidate;

    if (TryExtractV(candidate, out var v))
      return $"https://music.youtube.com/watch?v={Uri.EscapeDataString(v)}";

    if (TryNormalizeMusicUrl(candidate, out var musicUrl))
      return musicUrl;

    return "https://music.youtube.com/";
  }

  private static bool TryNormalizeMusicUrl(string candidate, out string url)
  {
    url = "";
    if (string.IsNullOrWhiteSpace(candidate)) return false;

    try
    {
      var uri = new Uri(candidate, UriKind.Absolute);
      if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        return false;

      if (!string.Equals(uri.Host, "music.youtube.com", StringComparison.OrdinalIgnoreCase))
        return false;

      url = uri.ToString();
      return true;
    }
    catch
    {
      return false;
    }
  }

  private static bool TryExtractV(string url, out string v)
  {
    v = "";
    if (string.IsNullOrWhiteSpace(url)) return false;

    try
    {
      var u = new Uri(url);
      var query = u.Query.TrimStart('?');
      if (string.IsNullOrWhiteSpace(query)) return false;

      foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
      {
        var kv = part.Split('=', 2);
        if (kv.Length != 2) continue;

        var key = Uri.UnescapeDataString(kv[0]);
        if (!string.Equals(key, "v", StringComparison.OrdinalIgnoreCase))
          continue;

        v = Uri.UnescapeDataString(kv[1]);
        return !string.IsNullOrWhiteSpace(v);
      }

      return false;
    }
    catch
    {
      return false;
    }
  }

  private static string Clamp(string s, int max)
  {
    s = s.Trim();
    return s.Length <= max ? s : s[..max];
  }

  private static string ResolveArtist(YtmState state)
  {
    if (!string.IsNullOrWhiteSpace(state.Artist))
      return state.Artist;

    if (!string.IsNullOrWhiteSpace(state.Album))
      return state.Album;

    return "YouTube Music";
  }

  private static string NormalizeAlbum(string? value, string title, string artist)
  {
    if (string.IsNullOrWhiteSpace(value))
      return "";

    var album = Clamp(value, 128);
    if (string.Equals(album, title, StringComparison.OrdinalIgnoreCase))
      return "";

    if (string.Equals(album, artist, StringComparison.OrdinalIgnoreCase))
      return "";

    return album;
  }

  private static string AppendAlbum(string artist, string album)
      => string.IsNullOrWhiteSpace(album) ? artist : $"{artist} · {album}";

  private void MarkDiscordOk()
  {
    DiscordOk = true;
    LastDiscordError = null;
    LastDiscordOkUtc = DateTimeOffset.UtcNow;
  }

  private void MarkDiscordError(Exception ex)
  {
    DiscordOk = false;
    LastDiscordError = ex.Message;
  }
}
