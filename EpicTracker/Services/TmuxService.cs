using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EpicTracker.Services;

public class TmuxService(ILogger<TmuxService> logger)
{
    public async Task SendKeys(string sessionName, string message, CancellationToken cancellationToken = default)
    {
        var escaped = EscapeForTmux(message.TrimEnd());

        await RunTmux($"send-keys -t {sessionName} {escaped} C-m", sessionName, cancellationToken);

        await Task.Delay(100, cancellationToken);

        await RunTmux($"send-keys -t {sessionName} C-m", sessionName, cancellationToken);
    }

    private async Task RunTmux(string args, string sessionName, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo("tmux", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment.Remove("PSMUX_SESSION");

        using var proc = Process.Start(psi)!;
        await proc.WaitForExitAsync(cancellationToken);

        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync(cancellationToken);
            logger.LogWarning("tmux send-keys failed for session {Session} (exit {Code}): {Error}", sessionName, proc.ExitCode, err);
        }
    }

    private static string EscapeForTmux(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "\"\"";

        if (!text.Contains('\''))
            return $"'{text}'";

        return "'" + text.Replace("'", "'\\''") + "'";
    }
}
