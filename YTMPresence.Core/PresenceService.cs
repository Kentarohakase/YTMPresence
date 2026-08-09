namespace YTMPresence.Core;

public sealed class PresenceService
{
  private readonly IDiscordPresenceClient _discord;
  private readonly AppSettings _settings;
  private readonly SemaphoreSlim _operationGate = new(1, 1);
  private readonly object _statusSync = new();

  private string? _lastPresenceKey;
  private DateTimeOffset _lastUpdateUtc = DateTimeOffset.MinValue;
  private DateTimeOffset _lastIncomingUtc = DateTimeOffset.MinValue;

  // Track/PlayState Change Detection
  private string? _lastSongKey;
  private bool? _lastIsPlaying;
  private DateTimeOffset? _pausedSinceUtc;

  // Verhindert, dass dieselbe abgelehnte Button-URL bei jedem Update erneut
  // protokolliert wird.
  private string? _lastRejectedButtonUrl;

  // Wie oft soll der "Zeit-Text" aktualisiert werden?
  // Beispiel: 10 => "01:20 / 03:45" wird nur alle 10 Sekunden neu gesetzt.
  private const int ProgressTextStepSeconds = 10;

  private bool _discordOk = true;
  private string? _lastDiscordError;
  private DateTimeOffset? _lastDiscordOkUtc;

  public bool DiscordOk
  {
    get { lock (_statusSync) return _discordOk; }
  }

  public string? LastDiscordError
  {
    get { lock (_statusSync) return _lastDiscordError; }
  }

  public DateTimeOffset? LastDiscordOkUtc
  {
    get { lock (_statusSync) return _lastDiscordOkUtc; }
  }

  public PresenceService(IDiscordPresenceClient discord, AppSettings settings)
  {
    _discord = discord;
    _settings = settings;
  }

  public (bool DiscordOk, string? LastDiscordError, DateTimeOffset? LastDiscordOkUtc) GetStatusSnapshot()
  {
    lock (_statusSync)
      return (_discordOk, _lastDiscordError, _lastDiscordOkUtc);
  }

  public async Task HandleAsync(YtmState state, CancellationToken ct)
  {
    await _operationGate.WaitAsync(ct);
    try
    {
      await HandleCoreAsync(state, ct);
    }
    finally
    {
      _operationGate.Release();
    }
  }

  private async Task HandleCoreAsync(YtmState state, CancellationToken ct)
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
    await _operationGate.WaitAsync(ct);
    try
    {
      await ClearIfIdleCoreAsync(ct);
    }
    finally
    {
      _operationGate.Release();
    }
  }

  private async Task ClearIfIdleCoreAsync(CancellationToken ct)
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
            State: PresenceText.Advertisement,
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

      // Kein CLEAR vor dem SET: SET_ACTIVITY ersetzt die Aktivität von sich
      // aus. Der frühere "Discord Cache Fix" sendete ein Löschen und wartete
      // 200 ms - wirkungslos, solange das Löschen den nicht existierenden
      // Befehl CLEAR_ACTIVITY benutzte. Seit das Löschen tatsächlich
      // funktioniert, würde derselbe Ablauf die Presence bei jedem Track- und
      // Play/Pause-Wechsel kurz verschwinden lassen.
      await _discord.SetActivityAsync(activity, ct);

      _lastPresenceKey = key;
      _lastUpdateUtc = now;

      MarkDiscordOk();
    }
    catch (Exception ex)
    {
      MarkDiscordError(ex);
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
        SmallText: state.IsPlaying ? PresenceText.Playing : PresenceText.Paused
    );

    // Discord lehnt die *gesamte* Aktivität ab, wenn ein einzelner Button eine
    // ungültige URL trägt - dann erscheint gar keine Presence. Ein fragwürdiger
    // Link darf deshalb höchstens sich selbst kosten, nicht die Anzeige.
    var buttons = new List<DiscordButton>();

    if (IsValidButtonUrl(trackUrl))
    {
      buttons.Add(new DiscordButton(PresenceText.OpenOnYouTubeMusic, trackUrl));
    }
    else if (!string.Equals(trackUrl, _lastRejectedButtonUrl, StringComparison.Ordinal))
    {
      _lastRejectedButtonUrl = trackUrl;
      Logger.Warn($"Track-Button weggelassen: '{trackUrl}' ist keine gültige Button-URL.");
    }

    buttons.Add(new DiscordButton("YouTube Music", "https://music.youtube.com/"));

    // Zeit im Status: "Artist · 01:23 / 03:45" bei laufender Wiedergabe,
    // sonst der übersetzte Pause-Text aus PresenceText.
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
          ? PresenceText.PausedAt(Fmt(pos), artist)
          : PresenceText.PausedWithoutTime(AppendAlbum(artist, album));
    }

    return new DiscordActivity(
        Details: title,
        State: stateLine,
        Timestamps: timestamps,
        Assets: assets,
        Buttons: buttons.ToArray(),
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

  /// <summary>
  /// Prüft, ob eine URL als Discord-Button taugt.
  ///
  /// <see cref="Uri.TryCreate(string, UriKind, out Uri)"/> allein reicht nicht:
  /// Die Methode akzeptiert auch Zeichenketten mit Leerzeichen. Discord
  /// verlangt eine bereits korrekt kodierte URI und weist sonst die komplette
  /// Aktivität ab ("url must be a valid uri").
  /// </summary>
  internal static bool IsValidButtonUrl(string? url)
  {
    if (string.IsNullOrWhiteSpace(url) || url.Length > 512)
      return false;

    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
      return false;

    if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    foreach (var c in url)
    {
      if (char.IsWhiteSpace(c) || char.IsControl(c))
        return false;
    }

    return true;
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

      // AbsoluteUri behaelt die Prozent-Kodierung; ToString() wuerde sie
      // aufloesen und damit eine ungueltige URI erzeugen.
      url = uri.AbsoluteUri;
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
    lock (_statusSync)
    {
      _discordOk = true;
      _lastDiscordError = null;
      _lastDiscordOkUtc = DateTimeOffset.UtcNow;
    }
  }

  private void MarkDiscordError(Exception ex)
  {
    lock (_statusSync)
    {
      _discordOk = false;
      _lastDiscordError = ex.Message;
    }
  }
}
