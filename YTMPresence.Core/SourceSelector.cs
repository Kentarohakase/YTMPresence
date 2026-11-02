namespace YTMPresence.Core;

/// <summary>
/// Wählt unter mehreren Extension-Verbindungen genau eine Quelle aus.
///
/// Jede Verbindung ist ein Browserprofil; innerhalb eines Profils wählt die
/// Extension schon einen Tab aus. Sind Chrome und Edge oder zwei Profile
/// gleichzeitig verbunden, überschrieben sich ihre Zustände bisher gegenseitig,
/// und ein Befehl aus dem Mini-Player erreichte alle.
///
/// Die Regel ist dieselbe wie in der Extension: Eine spielende Quelle behält
/// die Auswahl. Pausiert sie, übernimmt eine andere, die gerade spielt. Spielt
/// keine, bleibt die bisherige ausgewählt. Fällt die ausgewählte weg, übernimmt
/// zuerst eine spielende, sonst die zuletzt spielende.
///
/// Nicht threadsicher: Der Aufrufer serialisiert.
/// </summary>
internal sealed class SourceSelector
{
  /// <summary>
  /// Eine spielende Quelle meldet sich etwa jede Sekunde. Bleibt sie länger
  /// still, zählt ihr letzter Zustand nicht mehr als "spielt gerade".
  /// </summary>
  internal static readonly TimeSpan PlayingFreshness = TimeSpan.FromSeconds(15);

  private readonly Dictionary<int, Entry> _sources = new();

  private sealed record Entry(YtmState State, string Label, DateTimeOffset LastSeenUtc, DateTimeOffset? LastPlayingUtc);

  public int? SelectedId { get; private set; }

  public string? SelectedLabel
    => SelectedId is int id && _sources.TryGetValue(id, out var entry) ? entry.Label : null;

  /// <summary>
  /// Nimmt den Zustand einer Quelle auf. Liefert den Zustand, der jetzt
  /// gelten soll, oder null, wenn eine andere Quelle ausgewählt bleibt.
  /// </summary>
  public YtmState? Update(int sourceId, string label, YtmState state, DateTimeOffset now)
  {
    _sources.TryGetValue(sourceId, out var previous);
    _sources[sourceId] = new Entry(state, label, now, state.IsPlaying ? now : previous?.LastPlayingUtc);

    if (SelectedId is not int selectedId || !_sources.TryGetValue(selectedId, out var selected))
    {
      SelectedId = sourceId;
      return state;
    }

    if (selectedId == sourceId)
    {
      if (state.IsPlaying)
        return state;

      // Die ausgewählte Quelle pausiert. Spielt eine andere gerade, übernimmt sie.
      if (FindPlaying(now, except: sourceId) is int playingId)
      {
        SelectedId = playingId;
        return _sources[playingId].State;
      }

      return state;
    }

    if (state.IsPlaying && !IsPlaying(selected, now))
    {
      SelectedId = sourceId;
      return state;
    }

    return null;
  }

  /// <summary>
  /// Entfernt eine Quelle. War sie ausgewählt, liefert die Methode den Zustand
  /// der neuen Auswahl, sonst null.
  /// </summary>
  public YtmState? Remove(int sourceId, DateTimeOffset now)
  {
    if (!_sources.Remove(sourceId) || SelectedId != sourceId)
      return null;

    SelectedId = FindPlaying(now, except: null) ?? FindMostRecentlyPlayed();
    return SelectedId is int id ? _sources[id].State : null;
  }

  private static bool IsPlaying(Entry entry, DateTimeOffset now)
    => entry.State.IsPlaying && now - entry.LastSeenUtc <= PlayingFreshness;

  private int? FindPlaying(DateTimeOffset now, int? except)
    => _sources
        .Where(pair => pair.Key != except && IsPlaying(pair.Value, now))
        .OrderByDescending(pair => pair.Value.LastSeenUtc)
        .ThenBy(pair => pair.Key)
        .Select(pair => (int?)pair.Key)
        .FirstOrDefault();

  private int? FindMostRecentlyPlayed()
    => _sources
        .OrderByDescending(pair => pair.Value.LastPlayingUtc ?? DateTimeOffset.MinValue)
        .ThenByDescending(pair => pair.Value.LastSeenUtc)
        .ThenBy(pair => pair.Key)
        .Select(pair => (int?)pair.Key)
        .FirstOrDefault();

  /// <summary>
  /// Kurze Bezeichnung für Diagnose und Log, etwa "Edge 155 #3". Chrome und
  /// Edge teilen sich die Extension-ID aus dem Chrome Web Store; unterscheiden
  /// lassen sie sich nur am User-Agent der Verbindung.
  /// </summary>
  internal static string DescribeSource(string? userAgent, int connectionId)
  {
    var browser =
        FindBrowser(userAgent, "Edg/", "Edge") ??
        FindBrowser(userAgent, "OPR/", "Opera") ??
        FindBrowser(userAgent, "Firefox/", "Firefox") ??
        FindBrowser(userAgent, "Chrome/", "Chrome") ??
        "Unknown browser";

    return $"{browser} #{connectionId}";
  }

  private static string? FindBrowser(string? userAgent, string token, string name)
  {
    if (string.IsNullOrEmpty(userAgent))
      return null;

    var index = userAgent.IndexOf(token, StringComparison.Ordinal);
    if (index < 0)
      return null;

    var start = index + token.Length;
    var end = start;
    while (end < userAgent.Length && char.IsAsciiDigit(userAgent[end]))
      end++;

    return end > start ? $"{name} {userAgent[start..end]}" : name;
  }
}
