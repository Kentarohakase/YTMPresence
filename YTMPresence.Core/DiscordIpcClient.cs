using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YTMPresence.Core;

public sealed class DiscordIpcClient : IDiscordPresenceClient, IAsyncDisposable
{
  private const int MaxFrameBytes = 10_000_000;
  internal const string UnavailableMessage = "Discord IPC is not reachable. Is the Discord desktop app running?";

  // Wenn Discord nicht läuft, würde jeder eingehende State sonst erneut alle
  // Pipes durchprobieren (bis zu ~5s). Mit einer kurzen Sperre bleibt der
  // Companion auch ohne Discord flüssig und verbindet sich automatisch neu.
  private static readonly TimeSpan ConnectCooldown = TimeSpan.FromSeconds(15);
  private DateTimeOffset _nextConnectAttemptUtc = DateTimeOffset.MinValue;

  // Discord antwortet normalerweise innerhalb von Millisekunden. Der
  // Companion-Server wartet pro eingehender Nachricht auf das Ergebnis,
  // deshalb bleibt die Wartezeit kurz.
  internal static readonly TimeSpan DefaultReplyTimeout = TimeSpan.FromSeconds(3);
  internal static readonly TimeSpan DefaultReadyTimeout = TimeSpan.FromSeconds(5);

  private readonly string _clientId;
  private readonly Func<CancellationToken, Task<Stream>> _connect;
  private readonly TimeSpan _replyTimeout;
  private readonly TimeSpan _readyTimeout;
  private TaskCompletionSource? _ready;

  private const int CloseOpcode = 2;
  private volatile string? _closeReason;

  // Erst nach READY verbunden. Der Datenstrom existiert schon vorher, deshalb
  // reicht "_stream ist gesetzt" als Prüfung nicht.
  private volatile bool _isReady;
  private readonly ConcurrentDictionary<string, TaskCompletionSource<DiscordCommandResult>> _pendingReplies =
      new(StringComparer.Ordinal);
  private Stream? _stream;
  private CancellationTokenSource? _connectionCts;
  private readonly SemaphoreSlim _connectLock = new(1, 1);
  private readonly SemaphoreSlim _sendLock = new(1, 1);

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };

  public DiscordIpcClient(string clientId)
    : this(clientId, ConnectAsync, DefaultReplyTimeout, DefaultReadyTimeout)
  {
  }

  /// <summary>Für Tests: ein eigener Datenstrom statt der Discord-Pipe.</summary>
  internal DiscordIpcClient(
    string clientId,
    Func<CancellationToken, Task<Stream>> connect,
    TimeSpan replyTimeout,
    TimeSpan readyTimeout)
  {
    _clientId = clientId;
    _connect = connect;
    _replyTimeout = replyTimeout;
    _readyTimeout = readyTimeout;
  }

  public async Task EnsureConnectedAsync(CancellationToken ct)
  {
    if (_isReady) return;

    await _connectLock.WaitAsync(ct);
    try
    {
      if (_isReady) return;

      // Während der Cooldown-Phase nicht erneut alle Pipes durchprobieren.
      if (DateTimeOffset.UtcNow < _nextConnectAttemptUtc)
        throw new InvalidOperationException(UnavailableMessage);

      try
      {
        _stream = await _connect(ct);
      }
      catch when (!ct.IsCancellationRequested)
      {
        _nextConnectAttemptUtc = DateTimeOffset.UtcNow + ConnectCooldown;
        throw;
      }

      _connectionCts = new CancellationTokenSource();
      _ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      var ready = _ready.Task;

      try
      {
        await SendHandshakeAsync(ct);

        // Discord verwirft Befehle, die vor READY ankommen, ohne jede Antwort.
        // Ohne dieses Warten ging die erste Presence nach jedem Verbinden
        // verloren; bei einem pausierten Track kam danach lange keine weitere.
        _ = Task.Run(() => DrainIncomingAsync(_connectionCts.Token));
        await ready.WaitAsync(_readyTimeout, ct);
      }
      catch (TimeoutException)
      {
        await ResetAsync();
        _nextConnectAttemptUtc = DateTimeOffset.UtcNow + ConnectCooldown;
        throw new InvalidOperationException("Discord did not confirm the connection.");
      }
      catch
      {
        await ResetAsync();
        _nextConnectAttemptUtc = DateTimeOffset.UtcNow + ConnectCooldown;
        throw;
      }

      // Verbindung steht: Cooldown zurücksetzen.
      _nextConnectAttemptUtc = DateTimeOffset.MinValue;
      _isReady = true;
    }
    finally
    {
      _connectLock.Release();
    }
  }

  public Task<DiscordCommandResult> SetActivityAsync(DiscordActivity activity, CancellationToken ct)
  {
    var nonce = NewNonce();
    var payload = new
    {
      cmd = "SET_ACTIVITY",
      args = new { pid = Environment.ProcessId, activity },
      nonce
    };
    return SendCommandAsync(nonce, JsonSerializer.Serialize(payload, JsonOptions), ct);
  }

  public Task<DiscordCommandResult> ClearActivityAsync(CancellationToken ct)
  {
    var nonce = NewNonce();
    return SendCommandAsync(nonce, BuildClearActivityJson(nonce), ct);
  }

  /// <summary>
  /// Baut die Nachricht zum Entfernen der Presence.
  ///
  /// Discords RPC kennt kein CLEAR_ACTIVITY - die dokumentierten Befehle sind
  /// SET_ACTIVITY, SEND_ACTIVITY_JOIN_INVITE und CLOSE_ACTIVITY_REQUEST.
  /// Geloescht wird ueber SET_ACTIVITY mit <c>activity: null</c>.
  ///
  /// Der Null-Wert muss dabei tatsaechlich im JSON stehen. Die normalen
  /// Optionen lassen Null-Felder weg (erwuenscht, damit optionale
  /// Activity-Felder nicht als null uebertragen werden) - hier wuerde dadurch
  /// aber genau die Angabe verschwinden, auf die es ankommt. Deshalb wird die
  /// Nachricht direkt zusammengesetzt statt serialisiert.
  /// </summary>
  internal static string BuildClearActivityJson(string nonce)
    => $$"""
      {"cmd":"SET_ACTIVITY","args":{"pid":{{Environment.ProcessId}},"activity":null},"nonce":"{{nonce}}"}
      """;

  // Hex ohne Bindestriche: braucht im handgebauten JSON keine Maskierung.
  private static string NewNonce() => Guid.NewGuid().ToString("N");

  /// <summary>
  /// Sendet einen Befehl und wartet auf die Antwort mit derselben Nonce.
  ///
  /// Früher galt ein Befehl als erfolgreich, sobald der Frame geschrieben war;
  /// eine Ablehnung landete nur im Log, und die Diagnose meldete weiter OK.
  /// Bleibt die Antwort aus, ist das ein eigenes Ergebnis und kein Fehler der
  /// Verbindung. Bricht die Verbindung vorher ab, wirft der Aufruf.
  /// </summary>
  private async Task<DiscordCommandResult> SendCommandAsync(string nonce, string json, CancellationToken ct)
  {
    var reply = new TaskCompletionSource<DiscordCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
    _pendingReplies[nonce] = reply;

    try
    {
      await SendRawJsonFrameAsync(1, json, ct);
      return await reply.Task.WaitAsync(_replyTimeout, ct);
    }
    catch (TimeoutException)
    {
      Logger.Warn($"Discord did not answer within {(int)_replyTimeout.TotalMilliseconds} ms.");
      return DiscordCommandResult.NoReply;
    }
    finally
    {
      _pendingReplies.TryRemove(nonce, out _);
    }
  }

  private Task SendHandshakeAsync(CancellationToken ct)
  {
    var payload = new { v = 1, client_id = _clientId };
    return SendJsonFrameAsync(0, payload, ct);
  }

  private async Task SendJsonFrameAsync(int opcode, object payload, CancellationToken ct)
  {
    if (_stream is null) throw new InvalidOperationException("Discord IPC is not connected.");

    await SendRawJsonFrameAsync(opcode, JsonSerializer.Serialize(payload, JsonOptions), ct);
  }

  private async Task SendRawJsonFrameAsync(int opcode, string json, CancellationToken ct)
  {
    if (_stream is null) throw new InvalidOperationException("Discord IPC is not connected.");

    var data = Encoding.UTF8.GetBytes(json);

    var header = new byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), opcode);
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), data.Length);

    await _sendLock.WaitAsync(ct);
    try
    {
      await _stream.WriteAsync(header.AsMemory(0, 8), ct);
      await _stream.WriteAsync(data.AsMemory(0, data.Length), ct);
      await _stream.FlushAsync(ct);
    }
    catch
    {
      await ResetAsync();
      throw;
    }
    finally
    {
      _sendLock.Release();
    }
  }

  private async Task DrainIncomingAsync(CancellationToken ct)
  {
    var header = new byte[8];

    while (!ct.IsCancellationRequested && _stream is not null)
    {
      try
      {
        var stream = _stream;
        if (stream is null)
          return;

        await ReadExactAsync(stream, header, ct);
        var len = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
        if (len < 0 || len > MaxFrameBytes) { await ResetAsync(); return; }

        var body = new byte[len];
        await ReadExactAsync(stream, body, ct);

        // Opcode 2: Discord beendet die Verbindung und nennt den Grund, etwa
        // "Invalid Client ID". Ohne ihn stünde in der Diagnose nur, dass die
        // Verbindung geschlossen wurde.
        if (BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4)) == CloseOpcode)
        {
          _closeReason = DescribeClose(body);
          Logger.Warn($"Discord closed the connection: {_closeReason}");
          await ResetAsync();
          return;
        }

        HandleReply(body);
      }
      catch
      {
        await ResetAsync();
        return;
      }
    }
  }

  /// <summary>
  /// Ordnet eine Antwort von Discord über ihre Nonce dem wartenden Befehl zu.
  ///
  /// Die eingehenden Frames wurden früher nur gelesen und verworfen. Dadurch
  /// blieb unbemerkt, dass Discord einen ungültigen Befehl ablehnte - genau so
  /// ist das nicht existierende CLEAR_ACTIVITY lange unentdeckt geblieben.
  ///
  /// Ein Fehlerframe gehört zu einer einzelnen Nachricht und lässt die
  /// Verbindung bestehen. Frames ohne wartenden Befehl, etwa READY nach dem
  /// Handshake, werden ignoriert; Fehler darunter nur protokolliert.
  /// </summary>
  private void HandleReply(byte[] body)
  {
    try
    {
      using var doc = JsonDocument.Parse(body);
      var root = doc.RootElement;
      if (root.ValueKind != JsonValueKind.Object)
        return;

      var command = TryGetString(root, "cmd");
      var evt = TryGetString(root, "evt");

      if (command == "DISPATCH" && evt == "READY")
      {
        _ready?.TrySetResult();
        return;
      }

      var isError = evt == "ERROR";
      var nonce = TryGetString(root, "nonce");

      TaskCompletionSource<DiscordCommandResult>? pending = null;
      if (nonce is not null)
        _pendingReplies.TryRemove(nonce, out pending);

      if (!isError)
      {
        pending?.TrySetResult(DiscordCommandResult.Accepted);
        return;
      }

      int? code = null;
      string? message = null;
      if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
      {
        if (data.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number && c.TryGetInt32(out var parsed))
          code = parsed;
        message = TryGetString(data, "message");
      }

      Logger.Warn($"Discord rejected the command '{command ?? "unknown"}' (code {code?.ToString() ?? "none"}): {message ?? "no reason given"}");
      pending?.TrySetResult(DiscordCommandResult.Rejected(code, message));
    }
    catch (JsonException)
    {
      // Kein auswertbares JSON: nicht jeder Frame ist eine Antwort.
    }
  }

  private static string DescribeClose(byte[] body)
  {
    try
    {
      using var doc = JsonDocument.Parse(body);
      var root = doc.RootElement;
      if (root.ValueKind == JsonValueKind.Object)
      {
        var message = TryGetString(root, "message") ?? "no reason given";
        return root.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number
            ? $"{message} (code {code.GetRawText()})"
            : message;
      }
    }
    catch (JsonException)
    {
    }

    return "no reason given";
  }

  private static string? TryGetString(JsonElement element, string name)
    => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString()
        : null;

  private static async Task ReadExactAsync(Stream s, byte[] buffer, CancellationToken ct)
  {
    var offset = 0;
    while (offset < buffer.Length)
    {
      var read = await s.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct);
      if (read == 0) throw new EndOfStreamException();
      offset += read;
    }
  }

  private static async Task<Stream> ConnectAsync(CancellationToken ct)
  {
    for (var i = 0; i < 10; i++)
    {
      try
      {
        if (OperatingSystem.IsWindows())
        {
          var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
          await pipe.ConnectAsync(500, ct);
          return pipe;
        }

        var path = ResolveUnixIpcPath(i);
        var sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await sock.ConnectAsync(new UnixDomainSocketEndPoint(path), ct);
        return new NetworkStream(sock, ownsSocket: true);
      }
      catch when (ct.IsCancellationRequested)
      {
        throw;
      }
      catch { }
    }

    throw new InvalidOperationException(UnavailableMessage);
  }

  private static string ResolveUnixIpcPath(int index)
  {
    var dirs = new[]
    {
            Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"),
            Environment.GetEnvironmentVariable("TMPDIR"),
            Environment.GetEnvironmentVariable("TMP"),
            Environment.GetEnvironmentVariable("TEMP"),
            "/tmp"
        }
    .Where(d => !string.IsNullOrWhiteSpace(d))
    .Select(d => d!)
    .Distinct();

    foreach (var d in dirs)
    {
      var p = Path.Combine(d, $"discord-ipc-{index}");
      if (File.Exists(p)) return p;
    }

    return Path.Combine("/tmp", $"discord-ipc-{index}");
  }

  private async Task ResetAsync()
  {
    _isReady = false;

    try
    {
      try { _connectionCts?.Cancel(); } catch { }

      if (_stream is IAsyncDisposable ad) await ad.DisposeAsync();
      else _stream?.Dispose();
    }
    catch { }
    finally
    {
      _stream = null;
      _connectionCts?.Dispose();
      _connectionCts = null;

      var closeReason = _closeReason;
      _closeReason = null;

      _ready?.TrySetException(new InvalidOperationException(closeReason is null
          ? "The connection to Discord closed before it was ready."
          : $"Discord closed the connection: {closeReason}"));
      _ready = null;

      // Antworten auf offene Befehle kommen über diese Verbindung nicht mehr.
      foreach (var nonce in _pendingReplies.Keys)
      {
        if (_pendingReplies.TryRemove(nonce, out var pending))
          pending.TrySetException(new InvalidOperationException(closeReason is null
              ? "The connection to Discord closed before it answered."
              : $"Discord closed the connection: {closeReason}"));
      }
    }
  }

  public async ValueTask DisposeAsync()
  {
    await ResetAsync();
    _connectLock.Dispose();
    _sendLock.Dispose();
  }
}
