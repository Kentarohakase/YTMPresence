using System.Net;

namespace YTMPresence.Core.Tests;

/// <summary>
/// Deterministischer HttpMessageHandler für Update-Tests: bildet absolute URLs
/// auf Responder ab. Kein echter Netzwerkzugriff. Unbekannte URLs werfen,
/// damit Testfehler sofort sichtbar sind.
/// </summary>
internal sealed class RouteHandler : HttpMessageHandler
{
  private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes =
    new(StringComparer.OrdinalIgnoreCase);

  private int _requestCount;

  public int RequestCount => Volatile.Read(ref _requestCount);

  public Func<HttpRequestMessage, HttpResponseMessage> this[string url]
  {
    get => _routes[url];
    set => _routes[url] = value;
  }

  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
  {
    Interlocked.Increment(ref _requestCount);

    var url = request.RequestUri?.AbsoluteUri
      ?? throw new InvalidOperationException("Request without URI.");

    if (!_routes.TryGetValue(url, out var responder))
      throw new InvalidOperationException($"No test route configured for '{url}'.");

    return Task.FromResult(responder(request));
  }
}

internal static class Responses
{
  public static HttpResponseMessage Text(string content)
    => new(HttpStatusCode.OK) { Content = new StringContent(content) };

  public static HttpResponseMessage Bytes(byte[] content)
    => new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };

  /// <summary>Antwort, deren Content-Length-Header bewusst vom Inhalt abweicht.</summary>
  public static HttpResponseMessage BytesWithClaimedLength(byte[] content, long claimedLength)
  {
    var response = new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new ByteArrayContent(content)
    };
    response.Content.Headers.ContentLength = claimedLength;
    return response;
  }

  /// <summary>Antwort ohne Content-Length (nicht-seekbarer Stream).</summary>
  public static HttpResponseMessage UnknownLengthBytes(byte[] content)
    => Stream(new ScriptedStream(Chunk(content, 4 * 1024), afterChunks: null));

  /// <summary>Content-Length verspricht die volle Länge, geliefert wird weniger.</summary>
  public static HttpResponseMessage TruncatedBytes(byte[] full, int deliverOnly)
  {
    var response = new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StreamContent(new MemoryStream(full[..deliverOnly]))
    };
    response.Content.Headers.ContentLength = full.LongLength;
    return response;
  }

  public static HttpResponseMessage Stream(System.IO.Stream stream)
    => new(HttpStatusCode.OK) { Content = new StreamContent(stream) };

  public static HttpResponseMessage Redirect(string absoluteLocation)
  {
    var response = new HttpResponseMessage(HttpStatusCode.Found);
    response.Headers.TryAddWithoutValidation("Location", absoluteLocation);
    return response;
  }

  public static HttpResponseMessage RedirectRelative(string relativeLocation)
  {
    var response = new HttpResponseMessage(HttpStatusCode.Found);
    response.Headers.Location = new Uri(relativeLocation, UriKind.Relative);
    return response;
  }

  private static List<byte[]> Chunk(byte[] content, int chunkSize)
  {
    var chunks = new List<byte[]>();
    for (var offset = 0; offset < content.Length; offset += chunkSize)
      chunks.Add(content[offset..Math.Min(offset + chunkSize, content.Length)]);
    return chunks;
  }
}

/// <summary>
/// Nicht-seekbarer Stream, der vorbereitete Chunks liefert und danach ein
/// konfigurierbares Verhalten zeigt (sauberes EOF, Blockieren bis zur
/// Cancellation oder Exception) – für Abbruch-, Stall- und Netzwerkfehlertests.
/// </summary>
internal sealed class ScriptedStream : Stream
{
  private readonly Queue<byte[]> _chunks;
  private readonly Func<CancellationToken, Task>? _afterChunks;

  public ScriptedStream(IEnumerable<byte[]> chunks, Func<CancellationToken, Task>? afterChunks)
  {
    _chunks = new Queue<byte[]>(chunks);
    _afterChunks = afterChunks;
  }

  public override bool CanRead => true;
  public override bool CanSeek => false;
  public override bool CanWrite => false;
  public override long Length => throw new NotSupportedException();
  public override long Position
  {
    get => throw new NotSupportedException();
    set => throw new NotSupportedException();
  }

  public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
  {
    if (_chunks.Count > 0)
    {
      var chunk = _chunks.Dequeue();
      chunk.CopyTo(buffer);
      return chunk.Length;
    }

    if (_afterChunks is not null)
      await _afterChunks(ct);

    return 0;
  }

  public override int Read(byte[] buffer, int offset, int count)
    => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

  public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

  public override void Flush() { }
  public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
  public override void SetLength(long value) => throw new NotSupportedException();
  public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>IProgress ohne SynchronizationContext-Umweg (deterministisch im Test).</summary>
internal sealed class SynchronousProgress<T>(Action<T> callback) : IProgress<T>
{
  public void Report(T value) => callback(value);
}
