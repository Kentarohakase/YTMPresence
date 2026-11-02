using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace YTMPresence.Core;

public sealed class CompanionServer : IAsyncDisposable
{
  private const int MaxWebSocketMessageBytes = 256 * 1024;
  private const int MaxMetadataTextLength = 256;
  private const int MaxUrlLength = 2048;

  /// <summary>
  /// Frist, in der ein Socket ein gültiges Token vorzeigen muss. Danach wird er
  /// geschlossen, damit fremde Verbindungen nicht unbegrenzt offen bleiben.
  /// Großzügig bemessen: Die Extension meldet sich spätestens mit dem
  /// Keepalive nach 20 Sekunden.
  /// </summary>
  private static readonly TimeSpan UnauthenticatedSocketTimeout = TimeSpan.FromSeconds(60);

  /// <summary>
  /// WebSocket-Verbindungen unterliegen nicht der Same-Origin-Policy: Ohne
  /// Prüfung könnte jede besuchte Webseite den lokalen Companion ansprechen.
  /// Legitime Clients laufen im Extension-Kontext und senden entsprechend einen
  /// Extension-Origin; ein fehlender Origin stammt von lokalen Nicht-Browser-
  /// Clients (Tests, CLI), die ohnehin volle Benutzerrechte haben.
  /// </summary>
  private static readonly string[] AllowedOriginSchemes =
  [
    "chrome-extension",
    "moz-extension",
    "safari-web-extension"
  ];
  private static readonly UTF8Encoding StrictUtf8 = new(
      encoderShouldEmitUTF8Identifier: false,
      throwOnInvalidBytes: true);

  private readonly AppSettings _settings;
  private readonly DiscordIpcClient _discord;
  private readonly PresenceService _presence;

  private WebApplication? _app;
  private CancellationTokenSource? _cts;
  private Task? _idleTask;
  private int _isRunning;
  private bool _disposed;
  private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

  private int _connectedClients;
  private int _unauthorizedCount;
  private int _rejectedOriginCount;
  private DateTimeOffset? _lastUnauthorizedUtc;
  private readonly object _statusSync = new();

  private DateTimeOffset? _lastMessageUtc;
  private string? _lastTitle;
  private string? _lastArtist;
  private string? _lastAlbum;
  private string? _lastAlbumArtUrl;
  private string? _lastTrackUrl;
  private bool? _lastIsPlaying;
  private double? _lastPositionSeconds;
  private double? _lastDurationSeconds;

  private readonly ConcurrentDictionary<int, WebSocket> _activeSockets = new();
  private readonly ConcurrentDictionary<int, WebSocket> _authenticatedSockets = new();
  private int _socketIdCounter;

  // Genau eine Verbindung bestimmt Presence, Mini-Player und Befehlsziel.
  private readonly SourceSelector _sources = new();
  private readonly object _sourceSync = new();

  public CompanionServer(AppSettings settings)
  {
    _settings = settings;
    _discord = new DiscordIpcClient(settings.DiscordClientId);
    _presence = new PresenceService(_discord, settings);
  }

  public bool IsRunning => Volatile.Read(ref _isRunning) == 1;

  public CompanionStatus GetStatusSnapshot()
  {
    var discordStatus = _presence.GetStatusSnapshot();

    string? selectedSource;
    string? selectedExtensionVersion;
    lock (_sourceSync)
    {
      selectedSource = _sources.SelectedLabel;
      selectedExtensionVersion = _sources.SelectedExtensionVersion;
    }

    lock (_statusSync)
    {
      return new CompanionStatus(
          IsRunning: IsRunning,
          ConnectedClients: Volatile.Read(ref _connectedClients),
          LastMessageUtc: _lastMessageUtc,
          LastTitle: _lastTitle,
          LastArtist: _lastArtist,
          LastAlbum: _lastAlbum,
          LastAlbumArtUrl: _lastAlbumArtUrl,
          LastTrackUrl: GetBestTrackUrl(state: null, fallbackUrl: _lastTrackUrl),
          LastIsPlaying: _lastIsPlaying,
          LastPositionSeconds: _lastPositionSeconds,
          LastDurationSeconds: _lastDurationSeconds,
          UnauthorizedMessages: Volatile.Read(ref _unauthorizedCount),
          LastUnauthorizedUtc: _lastUnauthorizedUtc,
          DiscordOk: discordStatus.DiscordOk,
          LastDiscordError: discordStatus.LastDiscordError,
          LastDiscordOkUtc: discordStatus.LastDiscordOkUtc,
          LastDiscordOutcome: discordStatus.LastDiscordOutcome,
          SelectedSource: selectedSource,
          DiscordNotRunning: discordStatus.DiscordNotRunning,
          SelectedExtensionVersion: selectedExtensionVersion
      );
    }
  }

  public async Task StartAsync()
  {
    await _lifecycleGate.WaitAsync();
    try
    {
      ObjectDisposedException.ThrowIf(_disposed, this);
      await StartCoreAsync();
    }
    finally
    {
      _lifecycleGate.Release();
    }
  }

  private async Task StartCoreAsync()
  {
    if (_app is not null)
      return;

    Logger.Info($"CompanionServer starting on {_settings.GetWebSocketEndpoint()}");

    _cts = new CancellationTokenSource();

    var builder = WebApplication.CreateSlimBuilder();
    builder.WebHost.UseUrls(_settings.GetListenUrl().ToString());

    var app = builder.Build();
    app.UseWebSockets();

    app.MapGet("/health", () => Results.Ok("ok"));

    app.Map(_settings.WebSocketPath, async (HttpContext ctx) =>
    {
      if (!ctx.WebSockets.IsWebSocketRequest)
      {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
      }

      var origin = ctx.Request.Headers["Origin"].ToString();
      if (!IsAllowedOrigin(origin))
      {
        var rejected = Interlocked.Increment(ref _rejectedOriginCount);
        if (rejected <= 3 || rejected % 10 == 0)
          Logger.Warn($"Rejected WebSocket connection from disallowed origin '{origin}'. Total: {rejected}");

        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
      }

      using var ws = await ctx.WebSockets.AcceptWebSocketAsync();

      var socketId = Interlocked.Increment(ref _socketIdCounter);
      _activeSockets[socketId] = ws;
      var sourceLabel = SourceSelector.DescribeSource(ctx.Request.Headers.UserAgent.ToString(), socketId);

      var countedClient = false;
      string? extensionVersion = null;

      // Eigene Frist bis zum ersten gültigen Token. Nach der Authentifizierung
      // wird sie abgeschaltet und wieder gegen RequestAborted gelaufen.
      using var authCts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
      authCts.CancelAfter(UnauthenticatedSocketTimeout);
      var authenticated = false;

      void MarkAuthenticated()
      {
        if (authenticated)
          return;

        authenticated = true;
        authCts.CancelAfter(Timeout.InfiniteTimeSpan);
      }

      Logger.Info("Extension socket connected.");

      try
      {
        var buffer = new byte[64 * 1024];

        while (ws.State == WebSocketState.Open && !ctx.RequestAborted.IsCancellationRequested)
        {
          var receiveToken = authenticated ? ctx.RequestAborted : authCts.Token;

          var json = await ReceiveTextAsync(ws, buffer, receiveToken);
          if (json is null)
            break;

          var (testOutcome, reportedVersion) = await TryHandleTestMessageAsync(ws, json, ctx.RequestAborted);
          if (testOutcome != TestMessageOutcome.NotATestMessage)
          {
            // Ein gültiges Token im Verbindungstest zählt als Nachweis: Die
            // Extension haelt die Verbindung bei pausierter Wiedergabe genau
            // darueber offen.
            if (testOutcome == TestMessageOutcome.ValidToken)
            {
              MarkAuthenticated();
              extensionVersion = reportedVersion ?? extensionVersion;
            }

            continue;
          }

          YtmState? state;
          try
          {
            state = JsonSerializer.Deserialize<YtmState>(json);
          }
          catch (Exception ex)
          {
            Logger.Warn($"Received invalid JSON: {ex.Message}");
            continue;
          }

          if (state is null)
            continue;

          if (!TokensMatch(state.Token, _settings.SecurityToken))
          {
            var invalidCount = Interlocked.Increment(ref _unauthorizedCount);
            lock (_statusSync)
              _lastUnauthorizedUtc = DateTimeOffset.UtcNow;

            if (invalidCount <= 3 || invalidCount % 10 == 0)
            {
              Logger.Warn($"Received invalid token. Total: {invalidCount}");
            }

            continue;
          }

          MarkAuthenticated();

          if (!TryNormalizeState(state, out var normalizedState))
            continue;

          state = normalizedState;

          if (!countedClient)
          {
            countedClient = true;
            Interlocked.Increment(ref _connectedClients);
            _authenticatedSockets[socketId] = ws;
            Logger.Info($"Extension client {sourceLabel} authenticated. Active clients: {Volatile.Read(ref _connectedClients)}");
          }

          YtmState? current;
          int? previousId;
          int? selectedId;
          lock (_sourceSync)
          {
            previousId = _sources.SelectedId;
            current = _sources.Update(socketId, sourceLabel, state, DateTimeOffset.UtcNow, extensionVersion);
            selectedId = _sources.SelectedId;
          }

          // Eine andere Verbindung ist ausgewählt: Dieser Zustand ist vermerkt,
          // ändert aber weder Presence noch Mini-Player.
          if (current is null)
            continue;

          if (previousId is not null && previousId != selectedId)
            Logger.Info($"Selected source switched to {DescribeSelectedSource()}.");

          await ApplyStateAsync(current, ctx.RequestAborted);
        }
      }
      finally
      {
        _activeSockets.TryRemove(socketId, out _);
        _authenticatedSockets.TryRemove(socketId, out _);

        if (countedClient)
        {
          Interlocked.Decrement(ref _connectedClients);
          Logger.Info($"Extension client {sourceLabel} disconnected. Active clients: {Volatile.Read(ref _connectedClients)}");
          await SelectFallbackSourceAsync(socketId);
        }
        else if (!authenticated && authCts.IsCancellationRequested && !ctx.RequestAborted.IsCancellationRequested)
        {
          Logger.Warn(
            $"Closed socket without a valid token after {UnauthenticatedSocketTimeout.TotalSeconds:0} seconds (origin '{origin}').");
        }
        else
        {
          Logger.Info("Unauthenticated extension socket disconnected.");
        }
      }
    });

    try
    {
      await app.StartAsync(_cts.Token);
      _app = app;
      Volatile.Write(ref _isRunning, 1);
    }
    catch
    {
      await app.DisposeAsync();
      _cts.Dispose();
      _cts = null;
      throw;
    }

    var serverCts = _cts!;
    _idleTask = RunIdleLoopAsync(serverCts.Token);

    Logger.Info("CompanionServer started successfully.");
  }

  private async Task ApplyStateAsync(YtmState state, CancellationToken ct)
  {
    lock (_statusSync)
    {
      _lastMessageUtc = DateTimeOffset.UtcNow;
      _lastTitle = state.Title;
      _lastArtist = state.Artist;
      _lastAlbum = state.Album;
      _lastAlbumArtUrl = state.AlbumArtUrl;
      _lastTrackUrl = GetBestTrackUrl(state, fallbackUrl: state.Url);
      _lastIsPlaying = state.IsPlaying;
      _lastPositionSeconds = state.PositionSeconds;
      _lastDurationSeconds = state.DurationSeconds;
    }

    await _presence.HandleAsync(state, ct);
  }

  /// <summary>
  /// Schließt die ausgewählte Verbindung, gilt sofort der letzte Zustand der
  /// neuen Auswahl. Sonst bliebe die Presence der getrennten Quelle stehen,
  /// bis die neue zufällig wieder etwas sendet; ein pausierter Tab tut das nicht.
  /// </summary>
  private async Task SelectFallbackSourceAsync(int closedSocketId)
  {
    YtmState? fallback;
    lock (_sourceSync)
      fallback = _sources.Remove(closedSocketId, DateTimeOffset.UtcNow);

    // Beim Beenden schließen alle Verbindungen nacheinander; keine davon soll
    // dabei noch eine Presence setzen.
    if (fallback is null || !IsRunning)
      return;

    Logger.Info($"Selected source switched to {DescribeSelectedSource()}.");

    try
    {
      using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      await ApplyStateAsync(fallback, timeout.Token);
    }
    catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
    {
      // Der Server wird gerade beendet.
    }
  }

  private string DescribeSelectedSource()
  {
    lock (_sourceSync)
      return _sources.SelectedLabel ?? "none";
  }

  private async Task RunIdleLoopAsync(CancellationToken ct)
  {
    while (!ct.IsCancellationRequested)
    {
      try
      {
        await Task.Delay(TimeSpan.FromSeconds(10), ct);
        await _presence.ClearIfIdleAsync(ct);
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested)
      {
        break;
      }
      catch (Exception ex)
      {
        Logger.Error(ex, "Error in the idle-clear background task.");
      }
    }
  }

  public async Task StopAsync(CancellationToken ct)
  {
    await _lifecycleGate.WaitAsync(ct);
    try
    {
      await StopCoreAsync(ct);
    }
    finally
    {
      _lifecycleGate.Release();
    }
  }

  private async Task StopCoreAsync(CancellationToken ct)
  {
    if (_app is null)
      return;

    var app = _app;
    var idleTask = _idleTask;

    Logger.Info("CompanionServer stopping.");

    try { _cts?.Cancel(); } catch { }
    Volatile.Write(ref _isRunning, 0);

    try
    {
      try
      {
        await CloseAllSocketsAsync();
        await app.StopAsync(ct);
      }
      finally
      {
        try
        {
          await app.DisposeAsync();
        }
        catch (Exception ex)
        {
          Logger.Error(ex, "Error disposing the companion web host.");
        }

        if (idleTask is not null)
        {
          try
          {
            await idleTask;
          }
          catch (OperationCanceledException)
          {
          }
        }
      }

      Logger.Info("CompanionServer stopped successfully.");
    }
    finally
    {
      _app = null;
      _idleTask = null;
      _cts?.Dispose();
      _cts = null;
    }
  }

  public async Task<bool> SendPlayerCommandAsync(string command, CancellationToken ct = default)
  {
    if (!IsAllowedPlayerCommand(command))
      return false;

    var message = JsonSerializer.Serialize(new
    {
      type = "YTM_COMMAND",
      command,
      ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    });

    // Nur die ausgewählte Verbindung bekommt den Befehl. Früher ging er an
    // alle: Mit Chrome und Edge gleichzeitig pausierte ein Klick beide.
    int? targetId;
    string? targetLabel;
    lock (_sourceSync)
    {
      targetId = _sources.SelectedId;
      targetLabel = _sources.SelectedLabel;
    }

    if (targetId is not int socketId || !_authenticatedSockets.TryGetValue(socketId, out var ws))
    {
      Logger.Info($"Player command '{command}' not sent: no selected source.");
      return false;
    }

    try
    {
      if (ws.State != WebSocketState.Open)
      {
        _authenticatedSockets.TryRemove(socketId, out _);
        return false;
      }

      await SendTextAsync(ws, message, ct);
      Logger.Info($"Player command '{command}' sent to {targetLabel}.");
      return true;
    }
    catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ConnectionAbortedException)
    {
      _authenticatedSockets.TryRemove(socketId, out _);
      Logger.Warn($"Player command '{command}' could not be sent to {targetLabel}: {ex.Message}");
      return false;
    }
  }

  private async Task CloseAllSocketsAsync()
  {
    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

    foreach (var pair in _activeSockets)
    {
      var ws = pair.Value;
      try
      {
        if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
          await ws.CloseAsync(
              WebSocketCloseStatus.NormalClosure,
              "Server shutting down",
              timeoutCts.Token);
        }
      }
      catch
      {
        // Best effort
      }
    }
  }

  private enum TestMessageOutcome
  {
    NotATestMessage,
    ValidToken,
    InvalidToken
  }

  private async Task<(TestMessageOutcome Outcome, string? ExtensionVersion)> TryHandleTestMessageAsync(
    WebSocket ws,
    string json,
    CancellationToken ct)
  {
    try
    {
      using var doc = JsonDocument.Parse(json);
      var root = doc.RootElement;

      if (!root.TryGetProperty("type", out var typeElement) ||
          !string.Equals(typeElement.GetString(), "YTM_TEST", StringComparison.Ordinal))
      {
        return (TestMessageOutcome.NotATestMessage, null);
      }

      var token = root.TryGetProperty("token", out var tokenElement)
          ? tokenElement.GetString()
          : null;

      // Optional: Ältere Extensions schicken keine Version.
      var extensionVersion = root.TryGetProperty("extensionVersion", out var versionElement) &&
          versionElement.ValueKind == JsonValueKind.String
          ? NormalizeExtensionVersion(versionElement.GetString())
          : null;

      var ok = TokensMatch(token, _settings.SecurityToken);

      if (!ok)
      {
        var invalidCount = Interlocked.Increment(ref _unauthorizedCount);
        lock (_statusSync)
          _lastUnauthorizedUtc = DateTimeOffset.UtcNow;

        if (invalidCount <= 3 || invalidCount % 10 == 0)
          Logger.Warn($"Invalid token in connection test. Total: {invalidCount}");
      }

      var response = JsonSerializer.Serialize(new
      {
        type = "YTM_TEST_RESULT",
        ok,
        message = ok ? "ok" : "invalid-token",
        companionVersion = System.Reflection.CustomAttributeExtensions
          .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(CompanionServer).Assembly)
          ?.InformationalVersion
      });

      await SendTextAsync(ws, response, ct);
      return ok
          ? (TestMessageOutcome.ValidToken, extensionVersion)
          : (TestMessageOutcome.InvalidToken, null);
    }
    catch (Exception ex) when (ex is JsonException or InvalidOperationException)
    {
      return (TestMessageOutcome.NotATestMessage, null);
    }
  }

  /// <summary>
  /// Nimmt nur eine Versionsnummer wie "1.5.1" an. Der Wert landet in
  /// Diagnose und Bericht und soll dort nichts anderes transportieren.
  /// </summary>
  internal static string? NormalizeExtensionVersion(string? value)
  {
    if (string.IsNullOrEmpty(value) || value.Length > 32)
      return null;

    foreach (var c in value)
    {
      if (!char.IsAsciiDigit(c) && c != '.')
        return null;
    }

    return value;
  }

  /// <summary>
  /// Erkennt, dass der Start am belegten Port gescheitert ist. Kestrel meldet
  /// das als IOException mit einer AddressInUseException darunter.
  /// </summary>
  public static bool IsAddressInUse(Exception ex)
  {
    for (Exception? current = ex; current is not null; current = current.InnerException)
    {
      if (current is AddressInUseException ||
          current is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse })
      {
        return true;
      }
    }

    return false;
  }

  /// <summary>
  /// Prüft den Origin des WebSocket-Handshakes. Fehlender Origin ist zulässig
  /// (lokale Nicht-Browser-Clients); Browser senden hier immer einen Wert, und
  /// Web-Origins werden abgelehnt.
  /// </summary>
  public static bool IsAllowedOrigin(string? origin)
  {
    if (string.IsNullOrWhiteSpace(origin))
      return true;

    // Opaker Origin (sandboxed iframe, data:/file:-Kontext) ist kein gültiger URI.
    if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri))
      return false;

    foreach (var scheme in AllowedOriginSchemes)
    {
      if (string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase))
        return true;
    }

    return false;
  }

  private static async Task<string?> ReceiveTextAsync(WebSocket ws, byte[] buffer, CancellationToken ct)
  {
    using var payload = new MemoryStream(capacity: Math.Min(buffer.Length, MaxWebSocketMessageBytes));
    var receivedBytes = 0;

    while (true)
    {
      WebSocketReceiveResult result;

      try
      {
        result = await ws.ReceiveAsync(buffer, ct);
      }
      catch (ConnectionAbortedException)
      {
        return null;
      }
      catch (OperationCanceledException)
      {
        return null;
      }
      catch (WebSocketException)
      {
        return null;
      }

      if (result.MessageType == WebSocketMessageType.Close)
        return null;

      if (result.MessageType != WebSocketMessageType.Text)
        return null;

      receivedBytes += result.Count;
      if (receivedBytes > MaxWebSocketMessageBytes)
        return null;

      payload.Write(buffer, 0, result.Count);

      if (result.EndOfMessage)
      {
        try
        {
          return StrictUtf8.GetString(payload.GetBuffer(), 0, checked((int)payload.Length));
        }
        catch (DecoderFallbackException)
        {
          return null;
        }
      }
    }
  }

  private static Task SendTextAsync(WebSocket ws, string text, CancellationToken ct)
  {
    var bytes = Encoding.UTF8.GetBytes(text);
    return ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
  }

  private static string? GetBestTrackUrl(YtmState? state, string? fallbackUrl)
  {
    var candidates = new[] { state?.ShareUrl, fallbackUrl, state?.Url };
    foreach (var candidate in candidates)
    {
      if (TryNormalizeMusicUrl(candidate, out var normalized))
        return normalized;
    }

    return null;
  }

  private static bool TryNormalizeState(YtmState state, out YtmState normalized)
  {
    normalized = state;

    if (!string.Equals(state.Source, "ytmusic", StringComparison.Ordinal))
      return false;

    var title = NormalizeText(state.Title, MaxMetadataTextLength);
    if (!state.IsAd && string.IsNullOrWhiteSpace(title))
      return false;

    var artist = NormalizeText(state.Artist, MaxMetadataTextLength);
    var album = NormalizeOptionalText(state.Album, MaxMetadataTextLength);
    var trackUrl = GetBestTrackUrl(state, state.Url) ?? "https://music.youtube.com/";
    var albumArtUrl = NormalizeHttpsUrl(state.AlbumArtUrl);
    var duration = NormalizeDuration(state.DurationSeconds);
    var position = NormalizePosition(state.PositionSeconds, duration);

    normalized = state with
    {
      Source = "ytmusic",
      Title = title,
      Artist = artist,
      Album = album,
      AlbumArtUrl = albumArtUrl,
      PositionSeconds = position,
      DurationSeconds = duration,
      Url = trackUrl,
      ShareUrl = trackUrl,
      Token = null
    };

    return true;
  }

  private static string NormalizeText(string? value, int maxLength)
  {
    var normalized = (value ?? string.Empty).Trim();
    return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
  }

  private static string? NormalizeOptionalText(string? value, int maxLength)
  {
    var normalized = NormalizeText(value, maxLength);
    return normalized.Length == 0 ? null : normalized;
  }

  private static double? NormalizeDuration(double? value)
      => value is double duration && double.IsFinite(duration) && duration > 0
          ? duration
          : null;

  private static double? NormalizePosition(double? value, double? duration)
  {
    if (value is not double position || !double.IsFinite(position) || position < 0)
      return null;

    return duration is double max ? Math.Min(position, max) : position;
  }

  private static string? NormalizeHttpsUrl(string? value)
  {
    if (string.IsNullOrWhiteSpace(value) || value.Length > MaxUrlLength)
      return null;

    if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
        !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(uri.Host) ||
        !string.IsNullOrEmpty(uri.UserInfo) ||
        !uri.IsDefaultPort)
    {
      return null;
    }

    // AbsoluteUri statt ToString(): ToString() liefert laut Dokumentation die
    // *unescaped* kanonische Form ("All characters are unescaped except #, ?
    // and %"). Aus ?q=Sam%20Smith wird dabei ?q=Sam Smith - mit echten
    // Leerzeichen und damit keine gueltige URI mehr.
    return uri.AbsoluteUri;
  }

  private static bool TryNormalizeMusicUrl(string? value, out string normalized)
  {
    normalized = string.Empty;

    var httpsUrl = NormalizeHttpsUrl(value);
    if (httpsUrl is null || !Uri.TryCreate(httpsUrl, UriKind.Absolute, out var uri))
      return false;

    if (!string.Equals(uri.Host, "music.youtube.com", StringComparison.OrdinalIgnoreCase))
      return false;

    normalized = uri.AbsoluteUri;
    return true;
  }

  private static bool TokensMatch(string? providedToken, string expectedToken)
  {
    if (string.IsNullOrWhiteSpace(providedToken) || string.IsNullOrEmpty(expectedToken))
      return false;

    if (providedToken.Length != expectedToken.Length)
      return false;

    var providedBytes = Encoding.UTF8.GetBytes(providedToken);
    var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
    return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
  }

  private static bool IsAllowedPlayerCommand(string command)
  {
    return command is "play-pause" or "next" or "previous";
  }

  public async ValueTask DisposeAsync()
  {
    await _lifecycleGate.WaitAsync();
    try
    {
      if (_disposed)
        return;

      _disposed = true;
      try
      {
        await StopCoreAsync(CancellationToken.None);
      }
      catch (Exception ex)
      {
        Logger.Error(ex, "Error stopping CompanionServer during dispose.");
      }

      try
      {
        await _discord.DisposeAsync();
      }
      catch (Exception ex)
      {
        Logger.Error(ex, "Error disposing the Discord client.");
      }
    }
    finally
    {
      _lifecycleGate.Release();
    }
  }
}
