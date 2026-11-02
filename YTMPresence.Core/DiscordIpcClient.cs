using System.Buffers.Binary;
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

  private readonly string _clientId;
  private Stream? _stream;
  private CancellationTokenSource? _connectionCts;
  private readonly SemaphoreSlim _connectLock = new(1, 1);
  private readonly SemaphoreSlim _sendLock = new(1, 1);

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };

  public DiscordIpcClient(string clientId) => _clientId = clientId;

  public async Task EnsureConnectedAsync(CancellationToken ct)
  {
    if (_stream is not null) return;

    await _connectLock.WaitAsync(ct);
    try
    {
      if (_stream is not null) return;

      // Während der Cooldown-Phase nicht erneut alle Pipes durchprobieren.
      if (DateTimeOffset.UtcNow < _nextConnectAttemptUtc)
        throw new InvalidOperationException(UnavailableMessage);

      try
      {
        _stream = await ConnectAsync(ct);
      }
      catch when (!ct.IsCancellationRequested)
      {
        _nextConnectAttemptUtc = DateTimeOffset.UtcNow + ConnectCooldown;
        throw;
      }

      _connectionCts = new CancellationTokenSource();

      try
      {
        await SendHandshakeAsync(ct);
      }
      catch
      {
        await ResetAsync();
        _nextConnectAttemptUtc = DateTimeOffset.UtcNow + ConnectCooldown;
        throw;
      }

      // Verbindung steht: Cooldown zurücksetzen.
      _nextConnectAttemptUtc = DateTimeOffset.MinValue;
      _ = Task.Run(() => DrainIncomingAsync(_connectionCts.Token));
    }
    finally
    {
      _connectLock.Release();
    }
  }

  public async Task SetActivityAsync(DiscordActivity activity, CancellationToken ct)
  {
    var payload = new
    {
      cmd = "SET_ACTIVITY",
      args = new { pid = Environment.ProcessId, activity },
      nonce = Guid.NewGuid().ToString("N")
    };
    await SendJsonFrameAsync(1, payload, ct);
  }

  public async Task ClearActivityAsync(CancellationToken ct)
    => await SendRawJsonFrameAsync(1, BuildClearActivityJson(), ct);

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
  internal static string BuildClearActivityJson()
    => $$"""
      {"cmd":"SET_ACTIVITY","args":{"pid":{{Environment.ProcessId}},"activity":null},"nonce":"{{Guid.NewGuid():N}}"}
      """;

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
        LogErrorFrame(body);
      }
      catch
      {
        await ResetAsync();
        return;
      }
    }
  }

  /// <summary>
  /// Protokolliert Fehlerantworten von Discord.
  ///
  /// Die eingehenden Frames wurden bisher nur gelesen und verworfen. Dadurch
  /// blieb unbemerkt, dass Discord einen ungueltigen Befehl ablehnte - die App
  /// meldete weiterhin "Discord OK", waehrend die Presence unveraendert
  /// stehenblieb. Genau so ist das nicht existierende CLEAR_ACTIVITY lange
  /// unentdeckt geblieben.
  ///
  /// Bewusst nur Protokollierung: Ein Fehlerframe gehoert zu einer einzelnen
  /// Nachricht und soll die Verbindung nicht abreissen lassen.
  /// </summary>
  private static void LogErrorFrame(byte[] body)
  {
    try
    {
      using var doc = JsonDocument.Parse(body);
      var root = doc.RootElement;

      if (!root.TryGetProperty("evt", out var evt) ||
          !string.Equals(evt.GetString(), "ERROR", StringComparison.Ordinal))
      {
        return;
      }

      var command = root.TryGetProperty("cmd", out var cmd) ? cmd.GetString() : null;
      var message = root.TryGetProperty("data", out var data) && data.TryGetProperty("message", out var m)
          ? m.GetString()
          : null;

      Logger.Warn($"Discord rejected the command '{command ?? "unknown"}': {message ?? "no reason given"}");
    }
    catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
    {
      // Kein auswertbares JSON: nicht jeder Frame ist eine Fehlerantwort.
    }
  }

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
    }
  }

  public async ValueTask DisposeAsync()
  {
    await ResetAsync();
    _connectLock.Dispose();
    _sendLock.Dispose();
  }
}
