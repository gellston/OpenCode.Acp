using System.Diagnostics;
using System.Text;

namespace OpenCode.Acp.Transport;

internal sealed class StdioAcpTransport : IAcpTransport
{
    // ACP wire data is UTF-8. Do not emit a BOM to stdin.
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false, true);

    private readonly OpenCodeAcpOptions options_;
    private readonly SemaphoreSlim sendLock_ = new(1, 1);
    private Process? process_;
    private Task? stderrTask_;

    public StdioAcpTransport(OpenCodeAcpOptions options)
    {
        options_ = options;
    }

    public event EventHandler<string>? LogReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var startInfo = CreateStartInfo();
        process_ = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        if (!process_.Start())
            throw new InvalidOperationException("Failed to start opencode acp.");

        stderrTask_ = PumpStderrAsync(process_);
        return Task.CompletedTask;
    }

    public async Task SendLineAsync(string message, CancellationToken cancellationToken = default)
    {
        var process = GetProcess();
        await sendLock_.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await process.StandardInput.WriteLineAsync(message).ConfigureAwait(false);
            await process.StandardInput.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            sendLock_.Release();
        }
    }

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        var line = await GetProcess().StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        return RemoveBom(line);
    }

    public async ValueTask DisposeAsync()
    {
        var process = process_;
        process_ = null;

        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
            {
                process.StandardInput.Close();
                using var timeout = new CancellationTokenSource(options_.ShutdownTimeout);

                try
                {
                    await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
            }

            if (stderrTask_ is not null)
            {
                try { await stderrTask_.ConfigureAwait(false); } catch { }
            }
        }
        finally
        {
            process.Dispose();
            sendLock_.Dispose();
        }
    }

    private ProcessStartInfo CreateStartInfo()
    {
        var workingDirectory = options_.ProcessWorkingDirectory ?? Environment.CurrentDirectory;

        if (OperatingSystem.IsWindows() && options_.LaunchViaCmdOnWindows)
        {
            var info = CreateBaseStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe", workingDirectory);
            info.ArgumentList.Add("/d");
            info.ArgumentList.Add("/s");
            info.ArgumentList.Add("/c");
            info.ArgumentList.Add(BuildWindowsCommand());
            return info;
        }

        var direct = CreateBaseStartInfo(options_.ExecutablePath, workingDirectory);
        if (options_.PrintLogs)
            direct.ArgumentList.Add("--print-logs");
        direct.ArgumentList.Add("acp");
        return direct;
    }

    private static ProcessStartInfo CreateBaseStartInfo(string fileName, string workingDirectory)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8NoBom,
            StandardOutputEncoding = Utf8NoBom,
            StandardErrorEncoding = Utf8NoBom,
            CreateNoWindow = true
        };
    }

    private string BuildWindowsCommand()
    {
        var executable = options_.ExecutablePath.Contains(' ')
            ? $"\"{options_.ExecutablePath}\""
            : options_.ExecutablePath;

        var command = options_.PrintLogs
            ? $"{executable} --print-logs acp"
            : $"{executable} acp";

        // CHCP output is redirected so ACP stdout remains JSON only.
        return options_.ForceUtf8OnWindows
            ? $"chcp 65001 >nul 2>&1 && {command}"
            : command;
    }

    private static string? RemoveBom(string? line)
    {
        if (line is null)
            return null;

        // Normal UTF-8 BOM after correct decoding.
        line = line.TrimStart('\uFEFF');

        // Defensive cleanup if a BOM was already mojibake-decoded elsewhere.
        if (line.StartsWith("ï»¿", StringComparison.Ordinal))
            line = line[3..];

        return line;
    }

    private Process GetProcess()
    {
        if (process_ is null || process_.HasExited)
            throw new InvalidOperationException("OpenCode ACP process is not running.");
        return process_;
    }

    private async Task PumpStderrAsync(Process process)
    {
        while (!process.HasExited)
        {
            var line = await process.StandardError.ReadLineAsync().ConfigureAwait(false);
            if (line is null)
                break;
            LogReceived?.Invoke(this, line);
        }
    }
}
