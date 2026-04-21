using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace YTMPresence.Core;

public sealed class CompanionServer : IAsyncDisposable
{
  private readonly AppSettings _settings;
  private readonly DiscordIpcClient _discord;
  private readonly PresenceService _presence;

  private WebApplication? _app;
  private CancellationTokenSource? _cts;
  private int _isRunning;

  private int _connectedClients;
  private int _unauthorizedCount;
  private DateTimeOffset? _lastUnauthorizedUtc;

  private DateTimeOffset? _lastMessageUtc;
  private string? _lastTitle;
  private string? _lastArtist;
  private bool? _lastIsPlaying;

  private readonly ConcurrentDictionary<int, WebSocket> _activeSockets = new();
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
    return new CompanionStatus(
        IsRunning: IsRunning,
        ConnectedClients: Volatile.Read(ref _connectedClients),
        LastMessageUtc: _lastMessageUtc,
        LastTitle: _lastTitle,
        LastArtist: _lastArtist,
        LastIsPlaying: _lastIsPlaying,
        UnauthorizedMessages: Volatile.Read(ref _unauthorizedCount),
        LastUnauthorizedUtc: _lastUnauthorizedUtc,
        DiscordOk: _presence.DiscordOk,
        LastDiscordError: _presence.LastDiscordError,
        LastDiscordOkUtc: _presence.LastDiscordOkUtc
    );
  }

  public async Task StartAsync()
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

          if (string.IsNullOrWhiteSpace(state.Token) ||
              !string.Equals(state.Token, _settings.SecurityToken, StringComparison.Ordinal))
          {
            var invalidCount = Interlocked.Increment(ref _unauthorizedCount);
            _lastUnauthorizedUtc = DateTimeOffset.UtcNow;

            if (invalidCount <= 3 || invalidCount % 10 == 0)
            {
              Logger.Warn($"Ungültiges Token empfangen. Gesamtzahl: {invalidCount}");
            }

            continue;
          }

          if (!countedClient)
          {
            countedClient = true;
            Interlocked.Increment(ref _connectedClients);
            Logger.Info($"Extension Client authentifiziert. Clients aktiv: {Volatile.Read(ref _connectedClients)}");
          }

          _lastMessageUtc = DateTimeOffset.UtcNow;
          _lastTitle = state.Title;
          _lastArtist = state.Artist;
          _lastIsPlaying = state.IsPlaying;

          await _presence.HandleAsync(state, ctx.RequestAborted);
        }
      }
      finally
      {
        _activeSockets.TryRemove(socketId, out _);

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
    _ = Task.Run(async () =>
    {
      while (!serverCts.IsCancellationRequested)
      {
        try
        {
          await Task.Delay(TimeSpan.FromSeconds(10), serverCts.Token);
          await _presence.ClearIfIdleAsync(serverCts.Token);
        }
        catch (OperationCanceledException)
        {
          break;
        }
        catch (Exception ex)
        {
          Logger.Error(ex, "Fehler im Idle-Clear Hintergrundtask.");
        }
      }
    });

    Logger.Info("CompanionServer erfolgreich gestartet.");
  }

  public async Task StopAsync(CancellationToken ct)
  {
    if (_app is null)
      return;

    Logger.Info("CompanionServer wird gestoppt.");

    try { _cts?.Cancel(); } catch { }
    Volatile.Write(ref _isRunning, 0);

    try
    {
      await CloseAllSocketsAsync();
      await _app.StopAsync(ct);
      await _app.DisposeAsync();
      Logger.Info("CompanionServer erfolgreich gestoppt.");
    }
    finally
    {
      _app = null;
      _cts?.Dispose();
      _cts = null;
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

      var ok = !string.IsNullOrWhiteSpace(token) &&
          string.Equals(token, _settings.SecurityToken, StringComparison.Ordinal);

      if (!ok)
      {
        var invalidCount = Interlocked.Increment(ref _unauthorizedCount);
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
    catch (JsonException)
    {
      return false;
    }
  }

  private static async Task<string?> ReceiveTextAsync(WebSocket ws, byte[] buffer, CancellationToken ct)
  {
    var sb = new StringBuilder();

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

      sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

      if (result.EndOfMessage)
        return sb.ToString();
    }
  }

  private static Task SendTextAsync(WebSocket ws, string text, CancellationToken ct)
  {
    var bytes = Encoding.UTF8.GetBytes(text);
    return ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
  }

  public async ValueTask DisposeAsync()
  {
    try
    {
      await StopAsync(CancellationToken.None);
    }
    catch (Exception ex)
    {
      Logger.Error(ex, "Fehler beim Dispose von CompanionServer.");
    }

    await _discord.DisposeAsync();
  }
}
