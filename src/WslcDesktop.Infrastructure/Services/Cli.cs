using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace WslcDesktop.Services;

internal sealed record CliResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Succeeded => ExitCode == 0;

    public string ErrorText
    {
        get
        {
            string text = string.IsNullOrWhiteSpace(StdErr) ? StdOut : StdErr;
            return string.IsNullOrWhiteSpace(text) ? $"Exited with code {ExitCode}." : text.Trim();
        }
    }
}

/// <summary>
///     Runs a console tool with redirected streams and a time limit.
/// </summary>
internal static class Cli
{
    private const int ErrorFileNotFound = 2;

    public static async Task<CliResult> RunAsync(
        string fileName,
        string[] arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ProcessStartInfo startInfo = CreateStartInfo(fileName, arguments);
        if (environment is not null)
        {
            foreach ((string? key, string? value) in environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = new Process();

        try
        {
            process.StartInfo = startInfo;
            process.Start();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorFileNotFound)
        {
            throw new ToolNotFoundException($"Could not start '{fileName}': {ex.Message}");
        }
        catch (Win32Exception ex)
        {
            throw new CliException($"Could not start '{fileName}': {ex.Message}");
        }

        process.StandardInput.Close();

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout is { } limit)
        {
            deadline.CancelAfter(limit);
        }

        Task<string> stdOut = process.StandardOutput.ReadToEndAsync(deadline.Token);
        Task<string> stdErr = process.StandardError.ReadToEndAsync(deadline.Token);

        try
        {
            await process.WaitForExitAsync(deadline.Token);

            // A child process that inherited the pipes can keep them open after the process itself exits.
            return new CliResult(process.ExitCode, await stdOut, await stdErr);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (timeout is { } expired && !cancellationToken.IsCancellationRequested)
            {
                throw new CliException($"{Path.GetFileNameWithoutExtension(fileName)} did not finish within {Describe(expired)}.");
            }

            throw;
        }
    }

    /// <summary>
    ///     Runs the tool and returns its standard output.
    /// </summary>
    public static async Task<string> RunCheckedAsync(
        string fileName,
        string[] arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        CliResult result = await RunAsync(fileName, arguments, environment, timeout, cancellationToken);
        return result.Succeeded ? result.StdOut : throw new CliException(result.ErrorText);
    }

    internal static ProcessStartInfo CreateStartInfo(string fileName, string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            // wslc fails with ERROR_INVALID_HANDLE when the child inherits no usable stdin.
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static string Describe(TimeSpan duration) => duration.TotalMinutes >= 1
        ? Humanize.Count((int)duration.TotalMinutes, "minute")
        : Humanize.Count(Math.Max(1, (int)duration.TotalSeconds), "second");

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or AggregateException)
        {
        }
    }
}
