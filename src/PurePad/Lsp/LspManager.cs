using System.Diagnostics;
using PurePad.Diagnostics;
using PurePad.Formatting;

namespace PurePad.Lsp;

/// <summary>
/// Orchestrates language-server sessions for the editor: it spawns (and reuses) one server per
/// server-command + workspace-root, forwards open/change/close for the active document, and raises
/// diagnostics back on the UI thread. Servers are located on PATH; a missing server degrades
/// silently (the editor keeps its built-in checking). Phase 1 = diagnostics.
/// </summary>
public sealed class LspManager : IDisposable
{
    private sealed class Session
    {
        public required Process Process { get; init; }
        public required JsonRpcConnection Connection { get; init; }
        public required LspClient Client { get; init; }
        public Task InitTask { get; set; } = Task.CompletedTask;
        public readonly Dictionary<string, int> Versions = new(); // uri -> version
    }

    private readonly object _lock = new();
    private readonly Dictionary<string, Session> _sessions = new(); // key: command|args|root
    private readonly Dictionary<string, Session> _byDocument = new(); // path -> owning session
    private readonly SynchronizationContext _ui;
    private readonly HashSet<string> _reportedMissing = new(StringComparer.OrdinalIgnoreCase);

    public LspManager(SynchronizationContext? uiContext) => _ui = uiContext ?? new SynchronizationContext();

    /// <summary>Diagnostics for the file at this path (raised on the UI thread).</summary>
    public event Action<string, IReadOnlyList<Diagnostic>>? DiagnosticsReported;

    /// <summary>Raised (once per command) when a configured server is not found on PATH.</summary>
    public event Action<string>? ServerMissing;

    /// <summary>Whether a server is currently driving the document at <paramref name="path"/>.</summary>
    public bool IsActive(string path) => _byDocument.ContainsKey(path);

    /// <summary>Start (or reuse) a server for <paramref name="path"/> and open the document.</summary>
    public void OpenDocument(string path, LspSettings settings, string text)
    {
        string? exe = ServerLocator.Resolve(settings.Command);
        if (exe is null)
        {
            if (_reportedMissing.Add(settings.Command))
            {
                _ui.Post(_ => ServerMissing?.Invoke(settings.Command), null);
            }

            return;
        }

        string root = FindRoot(path, settings.RootMarkers);
        string key = $"{exe}|{string.Join(' ', settings.Args)}|{root}";
        string uri = ToUri(path);

        lock (_lock)
        {
            if (_byDocument.ContainsKey(path))
            {
                return; // already open
            }

            if (!_sessions.TryGetValue(key, out Session? session))
            {
                session = StartSession(exe, settings, root);
                if (session is null)
                {
                    return;
                }

                _sessions[key] = session;
            }

            _byDocument[path] = session;
            session.Versions[uri] = 1;

            Session s = session;
            s.InitTask = s.InitTask.ContinueWith(_ => s.Client.DidOpen(uri, settings.LanguageId, 1, text),
                TaskScheduler.Default);
        }
    }

    /// <summary>Push the latest text of an open document to its server.</summary>
    public void ChangeDocument(string path, string text)
    {
        lock (_lock)
        {
            if (!_byDocument.TryGetValue(path, out Session? session))
            {
                return;
            }

            string uri = ToUri(path);
            int version = session.Versions.TryGetValue(uri, out int v) ? v + 1 : 1;
            session.Versions[uri] = version;
            session.Client.DidChange(uri, version, text);
        }
    }

    /// <summary>Close a document on its server (does not stop the server).</summary>
    public void CloseDocument(string path)
    {
        lock (_lock)
        {
            if (_byDocument.Remove(path, out Session? session))
            {
                session.Client.DidClose(ToUri(path));
            }
        }
    }

    private Session? StartSession(string exe, LspSettings settings, string root)
    {
        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.Exists(root) ? root : Environment.CurrentDirectory,
            };
            foreach (string arg in settings.Args)
            {
                psi.ArgumentList.Add(arg);
            }

            var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            _ = process.StandardError.ReadToEndAsync(); // drain stderr so the server never blocks

            var connection = new JsonRpcConnection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
            var client = new LspClient(connection, ToUri(root));
            client.DiagnosticsPublished += (uri, diagnostics) =>
                _ui.Post(_ => DiagnosticsReported?.Invoke(FromUri(uri), diagnostics), null);

            var session = new Session { Process = process, Connection = connection, Client = client };
            session.InitTask = client.InitializeAsync();
            return session;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string FindRoot(string filePath, IReadOnlyList<string> markers)
    {
        string? dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        for (string? d = dir; d is not null; d = Path.GetDirectoryName(d))
        {
            foreach (string marker in markers)
            {
                if (File.Exists(Path.Combine(d, marker)) || Directory.Exists(Path.Combine(d, marker)))
                {
                    return d;
                }
            }
        }

        return dir ?? Environment.CurrentDirectory;
    }

    private static string ToUri(string path) => new Uri(Path.GetFullPath(path)).AbsoluteUri;

    private static string FromUri(string uri)
    {
        try { return Uri.UnescapeDataString(new Uri(uri).LocalPath); }
        catch (UriFormatException) { return uri; }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (Session session in _sessions.Values)
            {
                try
                {
                    _ = session.Client.ShutdownAsync();
                    session.Client.Dispose();
                    if (!session.Process.HasExited)
                    {
                        session.Process.Kill(entireProcessTree: true);
                    }

                    session.Process.Dispose();
                }
                catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
                {
                    // best-effort teardown
                }
            }

            _sessions.Clear();
            _byDocument.Clear();
        }
    }
}
