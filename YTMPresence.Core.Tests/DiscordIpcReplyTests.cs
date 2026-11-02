using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using YTMPresence.Core;

namespace YTMPresence.Core.Tests;

/// <summary>
/// Prueft, dass der Client Discords Antworten ueber die Nonce dem Befehl
/// zuordnet, statt einen Befehl nach dem Schreiben als erfolgreich zu werten.
/// Discords Seite der Verbindung wird dabei im Speicher nachgespielt.
/// </summary>
public class DiscordIpcReplyTests
{
  private static readonly DiscordActivity Activity = new("Song", "Artist");
  private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

  [Fact]
  public async Task Connect_WaitsForReady()
  {
    // Discord verwirft Befehle vor READY kommentarlos. Gegen das echte
    // Discord gemessen: ein SET_ACTIVITY direkt nach dem Handshake blieb ohne
    // Antwort, nach READY kam sie mit derselben Nonce zurueck.
    await using var discord = new FakeDiscord();
    await using var client = discord.CreateClient();

    var connect = client.EnsureConnectedAsync(CancellationToken.None);
    await discord.ReadHandshakeAsync();
    await Task.Delay(100);
    Assert.False(connect.IsCompleted, "Der Client darf vor READY nicht als verbunden gelten.");

    await discord.SendReadyAsync();
    await connect.WaitAsync(TestTimeout);
  }

  [Fact]
  public async Task SecondConnectCall_AlsoWaitsForReady()
  {
    // Der Datenstrom steht schon vor READY. Ein zweiter Aufruf darf deshalb
    // nicht schon an "Datenstrom vorhanden" vorbeikommen und vor READY senden.
    await using var discord = new FakeDiscord();
    await using var client = discord.CreateClient();

    var first = client.EnsureConnectedAsync(CancellationToken.None);
    await discord.ReadHandshakeAsync();
    var second = client.EnsureConnectedAsync(CancellationToken.None);
    await Task.Delay(100);
    Assert.False(second.IsCompleted, "Auch der zweite Aufruf muss auf READY warten.");

    await discord.SendReadyAsync();
    await Task.WhenAll(first, second).WaitAsync(TestTimeout);
  }

  [Fact]
  public async Task MissingReady_FailsTheConnection()
  {
    await using var discord = new FakeDiscord();
    await using var client = discord.CreateClient(readyTimeout: TimeSpan.FromMilliseconds(200));

    var connect = client.EnsureConnectedAsync(CancellationToken.None);
    await discord.ReadHandshakeAsync();

    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => connect.WaitAsync(TestTimeout));
    Assert.Contains("did not confirm", ex.Message);
  }

  [Fact]
  public async Task ConnectionClosedBeforeReady_FailsTheConnection()
  {
    await using var discord = new FakeDiscord();
    await using var client = discord.CreateClient();

    var connect = client.EnsureConnectedAsync(CancellationToken.None);
    await discord.ReadHandshakeAsync();
    await discord.CloseAsync();

    await Assert.ThrowsAsync<InvalidOperationException>(() => connect.WaitAsync(TestTimeout));
  }

  [Fact]
  public async Task ReplyWithMatchingNonce_IsAccepted()
  {
    await using var discord = new FakeDiscord();
    await using var client = await discord.ConnectClientAsync();

    var set = client.SetActivityAsync(Activity, CancellationToken.None);
    var command = await discord.ReadCommandAsync();
    await discord.ReplyAsync(command.Nonce);

    var result = await set.WaitAsync(TestTimeout);
    Assert.Equal("SET_ACTIVITY", command.Cmd);
    Assert.Equal(DiscordCommandOutcome.Accepted, result.Outcome);
  }

  [Fact]
  public async Task ErrorReply_IsRejectedWithCodeAndMessage()
  {
    await using var discord = new FakeDiscord();
    await using var client = await discord.ConnectClientAsync();

    var set = client.SetActivityAsync(Activity, CancellationToken.None);
    var command = await discord.ReadCommandAsync();
    await discord.ReplyAsync(command.Nonce, errorCode: 4000, errorMessage: "child \"activity\" fails");

    var result = await set.WaitAsync(TestTimeout);
    Assert.Equal(DiscordCommandOutcome.Rejected, result.Outcome);
    Assert.Equal(4000, result.ErrorCode);
    Assert.Equal("child \"activity\" fails", result.ErrorMessage);
  }

  [Fact]
  public async Task MissingReply_IsReportedAsNoReply()
  {
    await using var discord = new FakeDiscord();
    await using var client = await discord.ConnectClientAsync(replyTimeout: TimeSpan.FromMilliseconds(200));

    var set = client.SetActivityAsync(Activity, CancellationToken.None);
    await discord.ReadCommandAsync();

    var result = await set.WaitAsync(TestTimeout);
    Assert.Equal(DiscordCommandOutcome.NoReply, result.Outcome);
  }

  [Fact]
  public async Task ReplyForAnotherNonce_DoesNotDecideTheCommand()
  {
    await using var discord = new FakeDiscord();
    await using var client = await discord.ConnectClientAsync();

    var set = client.SetActivityAsync(Activity, CancellationToken.None);
    var command = await discord.ReadCommandAsync();
    await discord.ReplyAsync("someone-else", errorCode: 4000, errorMessage: "not yours");
    await discord.ReplyAsync(command.Nonce);

    var result = await set.WaitAsync(TestTimeout);
    Assert.Equal(DiscordCommandOutcome.Accepted, result.Outcome);
  }

  [Fact]
  public async Task ClearActivity_SendsNullActivity_AndUsesTheReply()
  {
    await using var discord = new FakeDiscord();
    await using var client = await discord.ConnectClientAsync();

    var clear = client.ClearActivityAsync(CancellationToken.None);
    var command = await discord.ReadCommandAsync();
    await discord.ReplyAsync(command.Nonce, errorCode: 4000, errorMessage: "cannot clear");

    var result = await clear.WaitAsync(TestTimeout);
    Assert.Equal(JsonValueKind.Null, command.Args.GetProperty("activity").ValueKind);
    Assert.Equal(DiscordCommandOutcome.Rejected, result.Outcome);
  }

  [Fact]
  public async Task EachCommand_GetsItsOwnNonce()
  {
    await using var discord = new FakeDiscord();
    await using var client = await discord.ConnectClientAsync();

    var set = client.SetActivityAsync(Activity, CancellationToken.None);
    var first = await discord.ReadCommandAsync();
    await discord.ReplyAsync(first.Nonce);
    await set.WaitAsync(TestTimeout);

    var clear = client.ClearActivityAsync(CancellationToken.None);
    var second = await discord.ReadCommandAsync();
    await discord.ReplyAsync(second.Nonce);
    await clear.WaitAsync(TestTimeout);

    Assert.False(string.IsNullOrWhiteSpace(first.Nonce));
    Assert.NotEqual(first.Nonce, second.Nonce);
  }

  [Fact]
  public async Task Rejection_KeepsTheConnectionUsable()
  {
    // Ein Fehlerframe gehoert zu einem einzelnen Befehl. Die Verbindung darf
    // daran nicht abreissen.
    await using var discord = new FakeDiscord();
    await using var client = await discord.ConnectClientAsync();

    var rejected = client.SetActivityAsync(Activity, CancellationToken.None);
    var first = await discord.ReadCommandAsync();
    await discord.ReplyAsync(first.Nonce, errorCode: 4000, errorMessage: "invalid");
    await rejected.WaitAsync(TestTimeout);

    var accepted = client.SetActivityAsync(Activity, CancellationToken.None);
    var second = await discord.ReadCommandAsync();
    await discord.ReplyAsync(second.Nonce);

    var result = await accepted.WaitAsync(TestTimeout);
    Assert.Equal(DiscordCommandOutcome.Accepted, result.Outcome);
  }

  [Fact]
  public async Task ConnectionClosedBeforeReply_Throws()
  {
    // Ohne Verbindung kommt keine Antwort mehr. Das ist ein Verbindungsfehler,
    // keine fehlende Antwort.
    await using var discord = new FakeDiscord();
    await using var client = await discord.ConnectClientAsync();

    var set = client.SetActivityAsync(Activity, CancellationToken.None);
    await discord.ReadCommandAsync();
    await discord.CloseAsync();

    await Assert.ThrowsAsync<InvalidOperationException>(() => set.WaitAsync(TestTimeout));
  }

  private sealed record Command(string Cmd, string Nonce, JsonElement Args);

  /// <summary>Discords Seite der IPC-Verbindung, im Speicher.</summary>
  private sealed class FakeDiscord : IAsyncDisposable
  {
    private readonly Pipe _toClient = new();
    private readonly Pipe _toDiscord = new();
    private readonly Stream _fromClient;
    private readonly Stream _toClientWriter;

    public FakeDiscord()
    {
      _fromClient = _toDiscord.Reader.AsStream();
      _toClientWriter = _toClient.Writer.AsStream();
    }

    public DiscordIpcClient CreateClient(TimeSpan? replyTimeout = null, TimeSpan? readyTimeout = null)
    {
      var clientStream = new DuplexStream(_toClient.Reader.AsStream(), _toDiscord.Writer.AsStream());
      return new DiscordIpcClient(
          "123",
          _ => Task.FromResult<Stream>(clientStream),
          replyTimeout ?? TimeSpan.FromSeconds(5),
          readyTimeout ?? TimeSpan.FromSeconds(5));
    }

    public async Task<DiscordIpcClient> ConnectClientAsync(TimeSpan? replyTimeout = null)
    {
      var client = CreateClient(replyTimeout);
      var connect = client.EnsureConnectedAsync(CancellationToken.None);

      await ReadHandshakeAsync();
      await SendReadyAsync();

      await connect.WaitAsync(TestTimeout);
      return client;
    }

    public async Task ReadHandshakeAsync()
    {
      var handshake = await ReadFrameAsync();
      Assert.Equal(0, handshake.Opcode);
      Assert.Equal("123", handshake.Json.GetProperty("client_id").GetString());
    }

    // Wie Discord: READY nach dem Handshake, ohne Nonce.
    public Task SendReadyAsync()
      => SendAsync(1, """{"cmd":"DISPATCH","evt":"READY","data":{"v":1}}""");

    public async Task<Command> ReadCommandAsync()
    {
      var frame = await ReadFrameAsync();
      Assert.Equal(1, frame.Opcode);
      return new Command(
          frame.Json.GetProperty("cmd").GetString()!,
          frame.Json.GetProperty("nonce").GetString()!,
          frame.Json.GetProperty("args"));
    }

    public Task ReplyAsync(string nonce, int? errorCode = null, string? errorMessage = null)
    {
      object reply = errorCode is null
          ? new { cmd = "SET_ACTIVITY", data = new { }, evt = (string?)null, nonce }
          : new { cmd = "SET_ACTIVITY", data = new { code = errorCode, message = errorMessage }, evt = "ERROR", nonce };

      return SendAsync(1, JsonSerializer.Serialize(reply));
    }

    public async Task CloseAsync() => await _toClient.Writer.CompleteAsync();

    private async Task<(int Opcode, JsonElement Json)> ReadFrameAsync()
    {
      var header = new byte[8];
      await ReadExactAsync(header);
      var opcode = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
      var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));

      var body = new byte[length];
      await ReadExactAsync(body);

      using var doc = JsonDocument.Parse(body);
      return (opcode, doc.RootElement.Clone());
    }

    private async Task ReadExactAsync(byte[] buffer)
    {
      var offset = 0;
      while (offset < buffer.Length)
      {
        var read = await _fromClient.ReadAsync(buffer.AsMemory(offset)).AsTask().WaitAsync(TestTimeout);
        if (read == 0)
          throw new EndOfStreamException("The client closed the connection.");
        offset += read;
      }
    }

    private async Task SendAsync(int opcode, string json)
    {
      var data = Encoding.UTF8.GetBytes(json);
      var header = new byte[8];
      BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), opcode);
      BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), data.Length);

      await _toClientWriter.WriteAsync(header);
      await _toClientWriter.WriteAsync(data);
      await _toClientWriter.FlushAsync();
    }

    public async ValueTask DisposeAsync()
    {
      await _toClient.Writer.CompleteAsync();
      await _toDiscord.Reader.CompleteAsync();
    }
  }

  /// <summary>Liest aus einem Datenstrom und schreibt in einen anderen.</summary>
  private sealed class DuplexStream(Stream read, Stream write) : Stream
  {
    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
      get => throw new NotSupportedException();
      set => throw new NotSupportedException();
    }

    public override void Flush() => write.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => write.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => read.Read(buffer, offset, count);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
      => read.ReadAsync(buffer, cancellationToken);

    public override void Write(byte[] buffer, int offset, int count) => write.Write(buffer, offset, count);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
      => write.WriteAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        read.Dispose();
        write.Dispose();
      }

      base.Dispose(disposing);
    }
  }
}
