using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace PurePad.Lsp;

/// <summary>
/// A JSON-RPC 2.0 connection over a pair of streams using LSP's <c>Content-Length</c> framing.
/// Correlates requests to responses by id, exposes server→client notifications and requests, and
/// runs a background receive loop. It is transport-only (no LSP semantics) and takes plain streams
/// so it can be driven by a child process's stdio or by in-memory pipes in tests.
/// </summary>
public sealed class JsonRpcConnection : IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Stream _input;
    private readonly Stream _output;
    private readonly object _writeLock = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly CancellationTokenSource _cts = new();
    private int _nextId;
    private volatile bool _disposed;

    public JsonRpcConnection(Stream input, Stream output)
    {
        _input = input;
        _output = output;
        _ = Task.Run(ReceiveLoop);
    }

    /// <summary>A server→client notification arrived: (method, params).</summary>
    public event Action<string, JsonElement>? NotificationReceived;

    /// <summary>
    /// A server→client request arrived. The handler returns the result payload (or null). If no
    /// handler is set, the connection auto-replies null, which keeps most servers happy.
    /// </summary>
    public Func<string, JsonElement, object?>? RequestReceived;

    /// <summary>Raised once when the connection closes (stream end or error).</summary>
    public event Action? Closed;

    public async Task<JsonElement> SendRequestAsync(string method, object? parameters, CancellationToken ct = default)
    {
        int id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        WriteMessage(new { jsonrpc = "2.0", id, method, @params = parameters });

        using (ct.Register(() => tcs.TrySetCanceled(ct)))
        {
            return await tcs.Task.ConfigureAwait(false);
        }
    }

    public void SendNotification(string method, object? parameters) =>
        WriteMessage(new { jsonrpc = "2.0", method, @params = parameters });

    private void WriteMessage(object message)
    {
        if (_disposed)
        {
            return;
        }

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        lock (_writeLock)
        {
            _output.Write(header, 0, header.Length);
            _output.Write(body, 0, body.Length);
            _output.Flush();
        }
    }

    private async Task ReceiveLoop()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                byte[]? body = await ReadMessageAsync(_cts.Token).ConfigureAwait(false);
                if (body is null)
                {
                    break; // end of stream
                }

                Dispatch(body);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // connection ended
        }
        finally
        {
            FaultPending();
            Closed?.Invoke();
        }
    }

    private void Dispatch(byte[] body)
    {
        using var doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        bool hasId = root.TryGetProperty("id", out JsonElement idEl);
        bool hasMethod = root.TryGetProperty("method", out JsonElement methodEl);

        if (hasId && !hasMethod)
        {
            // Response to one of our requests.
            if (idEl.TryGetInt32(out int id) && _pending.TryRemove(id, out var tcs))
            {
                if (root.TryGetProperty("error", out JsonElement err))
                {
                    tcs.TrySetException(new LspException(err.ToString()));
                }
                else
                {
                    JsonElement result = root.TryGetProperty("result", out JsonElement r) ? r.Clone() : default;
                    tcs.TrySetResult(result);
                }
            }

            return;
        }

        if (hasMethod)
        {
            string method = methodEl.GetString() ?? "";
            JsonElement prms = root.TryGetProperty("params", out JsonElement p) ? p.Clone() : default;

            if (hasId)
            {
                // Server→client request: reply so the server isn't blocked.
                object? result = RequestReceived?.Invoke(method, prms);
                WriteMessage(new { jsonrpc = "2.0", id = CloneId(idEl), result });
            }
            else
            {
                NotificationReceived?.Invoke(method, prms);
            }
        }
    }

    private static object CloneId(JsonElement idEl) =>
        idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt64(out long n) ? n : idEl.GetString() ?? "";

    private async Task<byte[]?> ReadMessageAsync(CancellationToken ct)
    {
        int contentLength = -1;
        string? line;
        while ((line = await ReadHeaderLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            if (line.Length == 0)
            {
                break; // blank line ends the header block
            }

            int colon = line.IndexOf(':');
            if (colon > 0 && line.AsSpan(0, colon).Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(line.AsSpan(colon + 1).Trim(), out contentLength);
            }
        }

        if (line is null || contentLength < 0)
        {
            return null;
        }

        var body = new byte[contentLength];
        int read = 0;
        while (read < contentLength)
        {
            int n = await _input.ReadAsync(body.AsMemory(read, contentLength - read), ct).ConfigureAwait(false);
            if (n == 0)
            {
                return null;
            }

            read += n;
        }

        return body;
    }

    private async Task<string?> ReadHeaderLineAsync(CancellationToken ct)
    {
        var sb = new StringBuilder(32);
        var one = new byte[1];
        while (true)
        {
            int n = await _input.ReadAsync(one.AsMemory(0, 1), ct).ConfigureAwait(false);
            if (n == 0)
            {
                return sb.Length == 0 ? null : sb.ToString();
            }

            if (one[0] == (byte)'\n')
            {
                return sb.ToString();
            }

            if (one[0] != (byte)'\r')
            {
                sb.Append((char)one[0]);
            }
        }
    }

    private void FaultPending()
    {
        foreach (var kv in _pending)
        {
            kv.Value.TrySetException(new LspException("Connection closed."));
        }

        _pending.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        FaultPending();
        _cts.Dispose();
    }
}

/// <summary>An error surfaced by a language server or its connection.</summary>
public sealed class LspException : Exception
{
    public LspException(string message) : base(message) { }
}
