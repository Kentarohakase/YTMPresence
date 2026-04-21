using System.Buffers.Binary;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YTMPresence.Core;

public sealed class DiscordIpcClient : IAsyncDisposable
{
  private const int MaxFrameBytes = 10_000_000;

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

      _stream = await ConnectAsync(ct);
      _connectionCts = new CancellationTokenSource();

      try
      {
        await SendHandshakeAsync(ct);
      }
      catch
      {
        await ResetAsync();
        throw;
      }

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
  {
    var payload = new
    {
      cmd = "CLEAR_ACTIVITY",
      args = new { pid = Environment.ProcessId },
      nonce = Guid.NewGuid().ToString("N")
    };
    await SendJsonFrameAsync(1, payload, ct);
  }

  private Task SendHandshakeAsync(CancellationToken ct)
  {
    var payload = new { v = 1, client_id = _clientId };
    return SendJsonFrameAsync(0, payload, ct);
  }

  private async Task SendJsonFrameAsync(int opcode, object payload, CancellationToken ct)
  {
    if (_stream is null) throw new InvalidOperationException("Discord IPC nicht verbunden.");

    var json = JsonSerializer.Serialize(payload, JsonOptions);
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
      }
      catch
      {
        await ResetAsync();
        return;
      }
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

    throw new InvalidOperationException("Discord IPC nicht erreichbar. Läuft Discord Desktop?");
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
