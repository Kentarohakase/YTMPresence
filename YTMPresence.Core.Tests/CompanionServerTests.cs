using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using YTMPresence.Core;

namespace YTMPresence.Core.Tests;

public class CompanionServerTests
{
  private const string Token = "test-token-123";

  private static int GetFreePort()
  {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
  }

  private static AppSettings ServerSettings(int port) => new()
  {
    ListenHost = "127.0.0.1",
    ListenPort = port,
    WebSocketPath = "/ws",
    SecurityToken = Token,
    // Ungültige Discord-Client-ID ist hier egal: ohne laufendes Discord
    // schlägt der IPC-Connect intern fehl und wird abgefangen.
    DiscordClientId = "0"
  };

  private static async Task<ClientWebSocket> ConnectAsync(int port, CancellationToken ct, string? origin = null)
  {
    var client = new ClientWebSocket();
    if (origin is not null)
      client.Options.SetRequestHeader("Origin", origin);

    await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws"), ct);
    return client;
  }

  private static async Task SendAsync(ClientWebSocket socket, string text, CancellationToken ct)
  {
    var bytes = Encoding.UTF8.GetBytes(text);
    await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
  }

  private static async Task SendFragmentedAsync(
    ClientWebSocket socket,
    byte[] payload,
    int firstFragmentLength,
    CancellationToken ct)
  {
    await socket.SendAsync(
      payload.AsMemory(0, firstFragmentLength),
      WebSocketMessageType.Text,
      endOfMessage: false,
      ct);
    await socket.SendAsync(
      payload.AsMemory(firstFragmentLength),
      WebSocketMessageType.Text,
      endOfMessage: true,
      ct);
  }

  private static async Task<string> ReceiveAsync(ClientWebSocket socket, CancellationToken ct)
  {
    var buffer = new byte[64 * 1024];
    var result = await socket.ReceiveAsync(buffer, ct);
    return Encoding.UTF8.GetString(buffer, 0, result.Count);
  }

  private static async Task PollUntilAsync(Func<bool> condition, TimeSpan timeout)
  {
    var deadline = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < deadline)
    {
      if (condition()) return;
      await Task.Delay(25);
    }
  }

  private static string StateJson(string token) => JsonSerializer.Serialize(new YtmState(
    Source: "ytmusic",
    Title: "Test Song",
    Artist: "Test Artist",
    Album: null,
    AlbumArtUrl: null,
    IsPlaying: true,
    PositionSeconds: null,
    DurationSeconds: null,
    Url: "https://music.youtube.com/watch?v=abc123",
    ShareUrl: null,
    IsAd: false,
    TimestampUnixMs: 0,
    Token: token));

  [Theory]
  // Kein Origin: lokale Nicht-Browser-Clients.
  [InlineData(null)]
  [InlineData("")]
  // Extension-Kontexte: Service Worker, Optionen, Popup.
  [InlineData("chrome-extension://abcdefghijklmnopabcdefghijklmnop")]
  [InlineData("moz-extension://0f9e8d7c-6b5a-4321-8765-0123456789ab")]
  [InlineData("safari-web-extension://ABCDEF01-2345-6789-ABCD-EF0123456789")]
  [InlineData("CHROME-EXTENSION://abcdefghijklmnopabcdefghijklmnop")]
  public void AllowedOrigins_AreAccepted(string? origin)
  {
    Assert.True(CompanionServer.IsAllowedOrigin(origin));
  }

  [Theory]
  // WebSockets unterliegen nicht der Same-Origin-Policy: Ohne diese Pruefung
  // koennte jede besuchte Seite den lokalen Companion ansprechen.
  [InlineData("https://evil.example")]
  [InlineData("http://localhost:3000")]
  [InlineData("https://music.youtube.com")]
  [InlineData("http://127.0.0.1:17373")]
  // Opaker Origin aus sandboxed iframe oder file:/data:-Kontext.
  [InlineData("null")]
  [InlineData("not a uri")]
  public void WebOrigins_AreRejected(string origin)
  {
    Assert.False(CompanionServer.IsAllowedOrigin(origin));
  }

  [Fact]
  public async Task Handshake_WithWebOrigin_IsRefused()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var port = GetFreePort();
    await using var server = new CompanionServer(ServerSettings(port));
    await server.StartAsync();

    await Assert.ThrowsAnyAsync<WebSocketException>(
      () => ConnectAsync(port, cts.Token, origin: "https://evil.example"));

    Assert.Equal(0, server.GetStatusSnapshot().ConnectedClients);
  }

  [Fact]
  public async Task Handshake_WithExtensionOrigin_IsAccepted()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var port = GetFreePort();
    await using var server = new CompanionServer(ServerSettings(port));
    await server.StartAsync();

    using var client = await ConnectAsync(
      port, cts.Token, origin: "chrome-extension://abcdefghijklmnopabcdefghijklmnop");
    await SendAsync(client, StateJson(Token), cts.Token);

    await PollUntilAsync(() => server.GetStatusSnapshot().ConnectedClients == 1, TimeSpan.FromSeconds(5));
    Assert.Equal(1, server.GetStatusSnapshot().ConnectedClients);
  }

  [Fact]
  public async Task TestMessage_WithValidToken_ReturnsOk()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var port = GetFreePort();
    await using var server = new CompanionServer(ServerSettings(port));
    await server.StartAsync();

    using var client = await ConnectAsync(port, cts.Token);
    await SendAsync(client, JsonSerializer.Serialize(new { type = "YTM_TEST", token = Token, ts = 0 }), cts.Token);
    var response = await ReceiveAsync(client, cts.Token);

    using var doc = JsonDocument.Parse(response);
    Assert.Equal("YTM_TEST_RESULT", doc.RootElement.GetProperty("type").GetString());
    Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
    Assert.Null(server.GetStatusSnapshot().LastMessageUtc);
  }

  [Fact]
  public async Task TestMessage_WithWrongToken_ReturnsNotOk()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var port = GetFreePort();
    await using var server = new CompanionServer(ServerSettings(port));
    await server.StartAsync();

    using var client = await ConnectAsync(port, cts.Token);
    await SendAsync(client, JsonSerializer.Serialize(new { type = "YTM_TEST", token = "wrong", ts = 0 }), cts.Token);
    var response = await ReceiveAsync(client, cts.Token);

    using var doc = JsonDocument.Parse(response);
    Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
    Assert.True(server.GetStatusSnapshot().UnauthorizedMessages >= 1);
  }

  [Fact]
  public async Task State_WithValidToken_CountsClientAndStoresTrack()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var port = GetFreePort();
    await using var server = new CompanionServer(ServerSettings(port));
    await server.StartAsync();

    using var client = await ConnectAsync(port, cts.Token);
    await SendAsync(client, StateJson(Token), cts.Token);

    await PollUntilAsync(() => server.GetStatusSnapshot().ConnectedClients >= 1, TimeSpan.FromSeconds(5));

    var snapshot = server.GetStatusSnapshot();
    Assert.Equal(1, snapshot.ConnectedClients);
    Assert.Equal("Test Song", snapshot.LastTitle);
  }

  [Fact]
  public async Task State_WithWrongToken_IsRejected()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var port = GetFreePort();
    await using var server = new CompanionServer(ServerSettings(port));
    await server.StartAsync();

    using var client = await ConnectAsync(port, cts.Token);
    await SendAsync(client, StateJson("wrong"), cts.Token);

    await PollUntilAsync(() => server.GetStatusSnapshot().UnauthorizedMessages >= 1, TimeSpan.FromSeconds(5));

    var snapshot = server.GetStatusSnapshot();
    Assert.True(snapshot.UnauthorizedMessages >= 1);
    Assert.Equal(0, snapshot.ConnectedClients);
  }

  [Fact]
  public async Task FragmentedUtf8State_SplitInsideMultibyteCharacter_IsDecodedCorrectly()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var port = GetFreePort();
    await using var server = new CompanionServer(ServerSettings(port));
    await server.StartAsync();

    using var client = await ConnectAsync(port, cts.Token);
    var json = StateJson(Token).Replace("Test Song", "Täst Song", StringComparison.Ordinal);
    var payload = Encoding.UTF8.GetBytes(json);
    var multibyteStart = Array.IndexOf(payload, (byte)0xC3);
    Assert.True(multibyteStart >= 0);

    await SendFragmentedAsync(client, payload, multibyteStart + 1, cts.Token);
    await PollUntilAsync(
      () => server.GetStatusSnapshot().LastTitle == "Täst Song",
      TimeSpan.FromSeconds(5));

    Assert.Equal("Täst Song", server.GetStatusSnapshot().LastTitle);
  }

  [Fact]
  public async Task OversizedMessage_IsRejectedWithoutUpdatingStatus()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var port = GetFreePort();
    await using var server = new CompanionServer(ServerSettings(port));
    await server.StartAsync();

    using var client = await ConnectAsync(port, cts.Token);
    var oversized = JsonSerializer.Serialize(new
    {
      source = "ytmusic",
      title = new string('x', 300 * 1024),
      artist = "Artist",
      isPlaying = true,
      url = "https://music.youtube.com/",
      isAd = false,
      ts = 0,
      token = Token
    });

    try
    {
      await SendAsync(client, oversized, cts.Token);
    }
    catch (WebSocketException)
    {
      // Der Server darf die Verbindung bereits während des Sends schließen.
    }

    await Task.Delay(100, cts.Token);
    Assert.Null(server.GetStatusSnapshot().LastMessageUtc);
  }

  [Fact]
  public async Task BinaryMessage_IsRejectedWithoutAuthentication()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var port = GetFreePort();
    await using var server = new CompanionServer(ServerSettings(port));
    await server.StartAsync();

    using var client = await ConnectAsync(port, cts.Token);
    var payload = Encoding.UTF8.GetBytes(StateJson(Token));
    await client.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, cts.Token);

    await Task.Delay(100, cts.Token);
    var snapshot = server.GetStatusSnapshot();
    Assert.Equal(0, snapshot.ConnectedClients);
    Assert.Null(snapshot.LastMessageUtc);
  }

  [Fact]
  public async Task AuthenticatedState_ClampsMetadataAndRejectsUnsafeUrls()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var port = GetFreePort();
    await using var server = new CompanionServer(ServerSettings(port));
    await server.StartAsync();

    using var client = await ConnectAsync(port, cts.Token);
    var state = new YtmState(
      Source: "ytmusic",
      Title: new string('T', 400),
      Artist: "Artist",
      Album: null,
      AlbumArtUrl: "file://attacker.invalid/share/cover.png",
      IsPlaying: true,
      PositionSeconds: 500,
      DurationSeconds: 120,
      Url: "javascript:alert(1)",
      ShareUrl: "file://attacker.invalid/share/track",
      IsAd: false,
      TimestampUnixMs: 0,
      Token: Token);

    await SendAsync(client, JsonSerializer.Serialize(state), cts.Token);
    await PollUntilAsync(
      () => server.GetStatusSnapshot().LastMessageUtc is not null,
      TimeSpan.FromSeconds(5));

    var snapshot = server.GetStatusSnapshot();
    Assert.Equal(256, snapshot.LastTitle?.Length);
    Assert.Null(snapshot.LastAlbumArtUrl);
    Assert.Equal("https://music.youtube.com/", snapshot.LastTrackUrl);
    Assert.Equal(120, snapshot.LastPositionSeconds);
  }

  [Fact]
  public async Task ConcurrentStartAndRestart_AreIdempotent()
  {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var port = GetFreePort();
    await using var server = new CompanionServer(ServerSettings(port));

    await Task.WhenAll(server.StartAsync(), server.StartAsync());
    Assert.True(server.IsRunning);

    await server.StopAsync(cts.Token);
    Assert.False(server.IsRunning);

    await server.StartAsync();
    Assert.True(server.IsRunning);
  }

  [Fact]
  public async Task Start_WhenPortIsOccupied_FailsAndLeavesServerStopped()
  {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();

    try
    {
      var port = ((IPEndPoint)listener.LocalEndpoint).Port;
      await using var server = new CompanionServer(ServerSettings(port));

      await Assert.ThrowsAnyAsync<Exception>(() => server.StartAsync());
      Assert.False(server.IsRunning);
    }
    finally
    {
      listener.Stop();
    }
  }
}
