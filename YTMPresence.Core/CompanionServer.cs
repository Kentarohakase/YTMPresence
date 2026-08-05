using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

using System.Collections.Concurrent;
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
          LastDiscordOkUtc: discordStatus.LastDiscordOkUtc
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

    Logger.Info($"CompanionServer startet auf {_settings.GetWebSocketEndpoint()}");

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

      using var ws = await ctx.WebSockets.AcceptWebSocketAsync();

      var socketId = Interlocked.Increment(ref _socketIdCounter);
      _activeSockets[socketId] = ws;

      var countedClient = false;

      Logger.Info("Extension Socket verbunden.");

      try
      {
        var buffer = new byte[64 * 1024];

        while (ws.State == WebSocketState.Open && !ctx.RequestAborted.IsCancellationRequested)
        {
          var json = await ReceiveTextAsync(ws, buffer, ctx.RequestAborted);
          if (json is null)
            break;

          if (await TryHandleTestMessageAsync(ws, json, ctx.RequestAborted))
            continue;

          YtmState? state;
          try
          {
            state = JsonSerializer.Deserialize<YtmState>(json);
          }
          catch (Exception ex)
          {
            Logger.Warn($"Ungültiges JSON empfangen: {ex.Message}");
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
              Logger.Warn($"Ungültiges Token empfangen. Gesamtzahl: {invalidCount}");
            }

            continue;
          }

          if (!TryNormalizeState(state, out var normalizedState))
            continue;

          state = normalizedState;

          if (!countedClient)
          {
            countedClient = true;
            Interlocked.Increment(ref _connectedClients);
            _authenticatedSockets[socketId] = ws;
            Logger.Info($"Extension Client authentifiziert. Clients aktiv: {Volatile.Read(ref _connectedClients)}");
          }

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

          await _presence.HandleAsync(state, ctx.RequestAborted);
        }
      }
      finally
      {
        _activeSockets.TryRemove(socketId, out _);
        _authenticatedSockets.TryRemove(socketId, out _);

        if (countedClient)
        {
          Interlocked.Decrement(ref _connectedClients);
          Logger.Info($"Extension Client getrennt. Clients aktiv: {Volatile.Read(ref _connectedClients)}");
        }
        else
        {
          Logger.Info("Nicht authentifizierter Extension Socket getrennt.");
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

    Logger.Info("CompanionServer erfolgreich gestartet.");
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
        Logger.Error(ex, "Fehler im Idle-Clear Hintergrundtask.");
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

    Logger.Info("CompanionServer wird gestoppt.");

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
          Logger.Error(ex, "Fehler beim Freigeben des Companion-Webhosts.");
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

      Logger.Info("CompanionServer erfolgreich gestoppt.");
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

    var sent = 0;

    foreach (var pair in _authenticatedSockets.ToArray())
    {
      var ws = pair.Value;

      try
      {
        if (ws.State != WebSocketState.Open)
        {
          _authenticatedSockets.TryRemove(pair.Key, out _);
          continue;
        }

        await SendTextAsync(ws, message, ct);
        sent++;
      }
      catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ConnectionAbortedException)
      {
        _authenticatedSockets.TryRemove(pair.Key, out _);
        Logger.Warn($"Player command '{command}' konnte nicht an Extension Socket {pair.Key} gesendet werden: {ex.Message}");
      }
    }

    if (sent > 0)
      Logger.Info($"Player command '{command}' an {sent} Extension Socket(s) gesendet.");

    return sent > 0;
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

  private async Task<bool> TryHandleTestMessageAsync(WebSocket ws, string json, CancellationToken ct)
  {
    try
    {
      using var doc = JsonDocument.Parse(json);
      var root = doc.RootElement;

      if (!root.TryGetProperty("type", out var typeElement) ||
          !string.Equals(typeElement.GetString(), "YTM_TEST", StringComparison.Ordinal))
      {
        return false;
      }

      var token = root.TryGetProperty("token", out var tokenElement)
          ? tokenElement.GetString()
          : null;

      var ok = TokensMatch(token, _settings.SecurityToken);

      if (!ok)
      {
        var invalidCount = Interlocked.Increment(ref _unauthorizedCount);
        lock (_statusSync)
          _lastUnauthorizedUtc = DateTimeOffset.UtcNow;

        if (invalidCount <= 3 || invalidCount % 10 == 0)
          Logger.Warn($"Ungültiges Token beim Verbindungstest. Gesamtzahl: {invalidCount}");
      }

      var response = JsonSerializer.Serialize(new
      {
        type = "YTM_TEST_RESULT",
        ok,
        message = ok ? "ok" : "invalid-token"
      });

      await SendTextAsync(ws, response, ct);
      return true;
    }
    catch (Exception ex) when (ex is JsonException or InvalidOperationException)
    {
      return false;
    }
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

    return uri.ToString();
  }

  private static bool TryNormalizeMusicUrl(string? value, out string normalized)
  {
    normalized = string.Empty;

    var httpsUrl = NormalizeHttpsUrl(value);
    if (httpsUrl is null || !Uri.TryCreate(httpsUrl, UriKind.Absolute, out var uri))
      return false;

    if (!string.Equals(uri.Host, "music.youtube.com", StringComparison.OrdinalIgnoreCase))
      return false;

    normalized = uri.ToString();
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
        Logger.Error(ex, "Fehler beim Stoppen von CompanionServer während Dispose.");
      }

      try
      {
        await _discord.DisposeAsync();
      }
      catch (Exception ex)
      {
        Logger.Error(ex, "Fehler beim Dispose des Discord-Clients.");
      }
    }
    finally
    {
      _lifecycleGate.Release();
    }
  }
}
