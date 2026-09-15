using System.Text.Json;
using PurePad.Diagnostics;

namespace PurePad.Lsp;

/// <summary>
/// A minimal Language Server Protocol client over a <see cref="JsonRpcConnection"/>: it performs the
/// initialize handshake, synchronizes open documents (full-text), and turns
/// <c>textDocument/publishDiagnostics</c> notifications into PurePad <see cref="Diagnostic"/>s.
/// Phase 1 covers diagnostics only; completion/hover/definition build on the same client later.
/// </summary>
public sealed class LspClient : IDisposable
{
    private readonly JsonRpcConnection _connection;
    private readonly string _rootUri;
    private bool _initialized;

    public LspClient(JsonRpcConnection connection, string rootUri)
    {
        _connection = connection;
        _rootUri = rootUri;
        _connection.NotificationReceived += OnNotification;
        _connection.RequestReceived = OnServerRequest;
    }

    /// <summary>Diagnostics were published for a document URI.</summary>
    public event Action<string, IReadOnlyList<Diagnostic>>? DiagnosticsPublished;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var capabilities = new
        {
            textDocument = new
            {
                synchronization = new { dynamicRegistration = false, didSave = true },
                publishDiagnostics = new { relatedInformation = false },
            },
        };

        await _connection.SendRequestAsync("initialize", new
        {
            processId = Environment.ProcessId,
            rootUri = _rootUri,
            capabilities,
            clientInfo = new { name = "PurePad", version = "1.0" },
        }, ct).ConfigureAwait(false);

        _connection.SendNotification("initialized", new { });
        _initialized = true;
    }

    public void DidOpen(string uri, string languageId, int version, string text)
    {
        if (!_initialized)
        {
            return;
        }

        _connection.SendNotification("textDocument/didOpen", new
        {
            textDocument = new { uri, languageId, version, text },
        });
    }

    public void DidChange(string uri, int version, string text)
    {
        if (!_initialized)
        {
            return;
        }

        _connection.SendNotification("textDocument/didChange", new
        {
            textDocument = new { uri, version },
            contentChanges = new object[] { new { text } }, // full-document sync
        });
    }

    public void DidClose(string uri)
    {
        if (!_initialized)
        {
            return;
        }

        _connection.SendNotification("textDocument/didClose", new { textDocument = new { uri } });
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        try
        {
            await _connection.SendRequestAsync("shutdown", null, ct).ConfigureAwait(false);
            _connection.SendNotification("exit", null);
        }
        catch (LspException)
        {
            // already gone
        }
    }

    private object? OnServerRequest(string method, JsonElement parameters) => method switch
    {
        // The server may ask for configuration/registration; a null reply is fine for Phase 1.
        _ => null,
    };

    private void OnNotification(string method, JsonElement parameters)
    {
        if (method != "textDocument/publishDiagnostics")
        {
            return;
        }

        if (!parameters.TryGetProperty("uri", out JsonElement uriEl) ||
            !parameters.TryGetProperty("diagnostics", out JsonElement diagsEl))
        {
            return;
        }

        var list = new List<Diagnostic>();
        foreach (JsonElement d in diagsEl.EnumerateArray())
        {
            list.Add(ToDiagnostic(d));
        }

        DiagnosticsPublished?.Invoke(uriEl.GetString() ?? "", list);
    }

    private static Diagnostic ToDiagnostic(JsonElement d)
    {
        int line = 1, character = 1;
        if (d.TryGetProperty("range", out JsonElement range) && range.TryGetProperty("start", out JsonElement start))
        {
            line = start.TryGetProperty("line", out JsonElement l) ? l.GetInt32() + 1 : 1;
            character = start.TryGetProperty("character", out JsonElement c) ? c.GetInt32() + 1 : 1;
        }

        DiagnosticSeverity severity = d.TryGetProperty("severity", out JsonElement sev)
            ? sev.GetInt32() switch { 1 => DiagnosticSeverity.Error, 2 => DiagnosticSeverity.Warning, _ => DiagnosticSeverity.Info }
            : DiagnosticSeverity.Warning;

        string message = d.TryGetProperty("message", out JsonElement m) ? m.GetString() ?? "" : "";
        string source = d.TryGetProperty("source", out JsonElement s) ? s.GetString() ?? "" : "";
        string full = source.Length > 0 ? $"{message} [{source}]" : message;

        return new Diagnostic(severity, line, character, full);
    }

    public void Dispose()
    {
        _connection.NotificationReceived -= OnNotification;
        _connection.Dispose();
    }
}
