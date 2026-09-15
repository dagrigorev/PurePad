namespace PurePad.Lsp;

/// <summary>Finds a language-server executable on the system PATH (honouring PATHEXT on Windows).</summary>
public static class ServerLocator
{
    /// <summary>Resolve <paramref name="command"/> to a full executable path on PATH, or null.</summary>
    public static string? Resolve(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        // An explicit path was given.
        if (Path.IsPathRooted(command))
        {
            return File.Exists(command) ? command : ResolveWithExtensions(command);
        }

        string[] dirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (string dir in dirs)
        {
            string candidate = Path.Combine(dir.Trim(), command);
            string? resolved = File.Exists(candidate) ? candidate : ResolveWithExtensions(candidate);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
    }

    private static string? ResolveWithExtensions(string baseCandidate)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        string[] exts = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (string ext in exts)
        {
            string candidate = baseCandidate + ext.Trim();
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
