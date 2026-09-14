using System.Diagnostics;

namespace PurePad.Tools;

/// <summary>Outcome of running an external tool: exit code and captured streams.</summary>
public sealed class ToolResult
{
    public ToolResult(int exitCode, string standardOutput, string standardError)
    {
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    public int ExitCode { get; }

    public string StandardOutput { get; }

    public string StandardError { get; }

    public bool Succeeded => ExitCode == 0;
}

/// <summary>Raised when an external tool cannot be launched (missing executable, timeout).</summary>
public sealed class ToolExecutionException : Exception
{
    public ToolExecutionException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>
/// Runs a console tool, piping the document to its standard input and capturing its
/// output. This is the single seam to the outside world, so every external formatter and
/// checker shares one hardened launcher (Single Responsibility).
/// </summary>
public sealed class ExternalToolRunner
{
    private readonly int _timeoutMilliseconds;

    public ExternalToolRunner(int timeoutMilliseconds = 15000)
    {
        _timeoutMilliseconds = timeoutMilliseconds;
    }

    public ToolResult Run(string command, IReadOnlyList<string> arguments, string input)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Read output asynchronously to avoid deadlocking on full pipe buffers.
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();

            process.StandardInput.Write(input);
            process.StandardInput.Close();

            if (!process.WaitForExit(_timeoutMilliseconds))
            {
                TryKill(process);
                throw new ToolExecutionException($"'{command}' did not finish within {_timeoutMilliseconds} ms.");
            }

            return new ToolResult(process.ExitCode, stdout.Result, stderr.Result);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new ToolExecutionException($"Could not start '{command}'. Is it installed and on PATH?", ex);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // The process may have exited between the timeout and the kill; ignore.
        }
    }
}
