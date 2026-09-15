using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using PurePad.Diagnostics;
using PurePad.Lsp;

namespace PurePad.Tests.Lsp;

public sealed class LspClientTests
{
    [Fact]
    public async Task Request_gets_correlated_response()
    {
        using var link = new DuplexLink();
        using var connection = new JsonRpcConnection(link.ClientIn, link.ClientOut);
        using var server = new FakeServer(link);
        server.OnRequest("ping", (_, respond) => respond(new { pong = true }));

        JsonElement result = await connection.SendRequestAsync("ping", new { }).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.GetProperty("pong").GetBoolean());
    }

    [Fact]
    public async Task Client_initializes_opens_and_receives_diagnostics()
    {
        using var link = new DuplexLink();
        using var connection = new JsonRpcConnection(link.ClientIn, link.ClientOut);
        var client = new LspClient(connection, "file:///root");
        using var server = new FakeServer(link);

        server.OnRequest("initialize", (_, respond) => respond(new { capabilities = new { } }));
        // When the document opens, the server publishes one error diagnostic.
        server.OnNotification("textDocument/didOpen", (p, send) =>
        {
            string uri = p.GetProperty("textDocument").GetProperty("uri").GetString()!;
            send("textDocument/publishDiagnostics", new
            {
                uri,
                diagnostics = new[]
                {
                    new
                    {
                        range = new { start = new { line = 2, character = 4 }, end = new { line = 2, character = 9 } },
                        severity = 1,
                        message = "boom",
                        source = "fake",
                    },
                },
            });
        });

        var received = new TaskCompletionSource<IReadOnlyList<Diagnostic>>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.DiagnosticsPublished += (_, diags) => received.TrySetResult(diags);

        await client.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
        client.DidOpen("file:///root/a.py", "python", 1, "code");

        IReadOnlyList<Diagnostic> diagnostics = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Diagnostic d = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal(3, d.Line);     // 0-based line 2 -> 1-based 3
        Assert.Equal(5, d.Column);   // 0-based char 4 -> 1-based 5
        Assert.Equal("boom [fake]", d.Message);

        client.Dispose();
    }

    // ----- test doubles ---------------------------------------------------

    /// <summary>Two in-memory streams wired as a bidirectional link (client streams vs server streams).</summary>
    private sealed class DuplexLink : IDisposable
    {
        private readonly ChannelStream _clientToServer = new();
        private readonly ChannelStream _serverToClient = new();

        public Stream ClientOut => _clientToServer;
        public Stream ClientIn => _serverToClient;
        public Stream ServerIn => _clientToServer;
        public Stream ServerOut => _serverToClient;

        public void Dispose()
        {
            _clientToServer.Complete();
            _serverToClient.Complete();
        }
    }

    /// <summary>A byte stream where writes on one thread are read on another (unbounded channel).</summary>
    private sealed class ChannelStream : Stream
    {
        private readonly Channel<byte[]> _chunks = Channel.CreateUnbounded<byte[]>();
        private byte[]? _current;
        private int _offset;

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public void Complete() => _chunks.Writer.TryComplete();

        public override void Write(byte[] buffer, int offset, int count)
        {
            var copy = new byte[count];
            Array.Copy(buffer, offset, copy, 0, count);
            _chunks.Writer.TryWrite(copy);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (_current is null || _offset >= _current.Length)
            {
                if (!await _chunks.Reader.WaitToReadAsync(ct))
                {
                    return 0;
                }

                _chunks.Reader.TryRead(out _current);
                _offset = 0;
            }

            int n = Math.Min(buffer.Length, _current!.Length - _offset);
            _current.AsSpan(_offset, n).CopyTo(buffer.Span);
            _offset += n;
            return n;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    /// <summary>A scripted LSP server: frames JSON-RPC, answers requests and reacts to notifications.</summary>
    private sealed class FakeServer : IDisposable
    {
        private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private readonly Stream _in;
        private readonly Stream _out;
        private readonly CancellationTokenSource _cts = new();
        private readonly Dictionary<string, Action<JsonElement, Action<object>>> _requests = new();
        private readonly Dictionary<string, Action<JsonElement, Action<string, object>>> _notifications = new();

        public FakeServer(DuplexLink link)
        {
            _in = link.ServerIn;
            _out = link.ServerOut;
            _ = Task.Run(Loop);
        }

        public void OnRequest(string method, Action<JsonElement, Action<object>> handler) => _requests[method] = handler;
        public void OnNotification(string method, Action<JsonElement, Action<string, object>> handler) => _notifications[method] = handler;

        private async Task Loop()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    byte[]? body = await ReadAsync(_cts.Token);
                    if (body is null) break;

                    using var doc = JsonDocument.Parse(body);
                    JsonElement root = doc.RootElement;
                    string method = root.GetProperty("method").GetString()!;
                    JsonElement prms = root.TryGetProperty("params", out var p) ? p : default;

                    if (root.TryGetProperty("id", out JsonElement id) && _requests.TryGetValue(method, out var rh))
                    {
                        rh(prms, result => Write(new { jsonrpc = "2.0", id = id.GetInt32(), result }));
                    }
                    else if (_notifications.TryGetValue(method, out var nh))
                    {
                        nh(prms, (m, prm) => Write(new { jsonrpc = "2.0", method = m, @params = prm }));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
            {
            }
        }

        private void Write(object message)
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, Json);
            byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");
            _out.Write(header);
            _out.Write(payload);
            _out.Flush();
        }

        private async Task<byte[]?> ReadAsync(CancellationToken ct)
        {
            int length = -1;
            string? line;
            while ((line = await ReadLineAsync(ct)) is { Length: > 0 })
            {
                int colon = line.IndexOf(':');
                if (colon > 0 && line[..colon].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(line[(colon + 1)..].Trim(), out length);
                }
            }

            if (line is null || length < 0) return null;

            var buf = new byte[length];
            int read = 0;
            while (read < length)
            {
                int n = await _in.ReadAsync(buf.AsMemory(read, length - read), ct);
                if (n == 0) return null;
                read += n;
            }

            return buf;
        }

        private async Task<string?> ReadLineAsync(CancellationToken ct)
        {
            var sb = new StringBuilder();
            var one = new byte[1];
            while (true)
            {
                int n = await _in.ReadAsync(one.AsMemory(0, 1), ct);
                if (n == 0) return sb.Length == 0 ? null : sb.ToString();
                if (one[0] == (byte)'\n') return sb.ToString();
                if (one[0] != (byte)'\r') sb.Append((char)one[0]);
            }
        }

        public void Dispose() => _cts.Cancel();
    }
}
