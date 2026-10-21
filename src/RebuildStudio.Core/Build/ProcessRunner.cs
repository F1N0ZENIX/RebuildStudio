using System.Diagnostics;

namespace RebuildStudio.Core.Build;

public sealed record ProcessResult(int ExitCode, bool TimedOut, IReadOnlyList<string> Lines);

/// <summary>
/// Executa ferramentas externas com ArgumentList (sem shell), timeout e captura de saída.
/// Não é um sandbox: limita o que o app dispara, mas o processo roda com as permissões do usuário.
/// </summary>
public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName, IEnumerable<string> args, string workingDirectory,
        Action<string>? onLine, TimeSpan timeout, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        psi.Environment["DOTNET_NOLOGO"] = "1";
        psi.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";

        var lines = new List<string>();
        var gate = new object();
        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

        void Handler(object? _, DataReceivedEventArgs e)
        {
            if (e.Data is null) return;
            lock (gate) lines.Add(e.Data);
            onLine?.Invoke(e.Data);
        }

        p.OutputDataReceived += Handler;
        p.ErrorDataReceived += Handler;
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await p.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch { /* já encerrado */ }
            var timedOut = !ct.IsCancellationRequested;
            lock (gate) return new ProcessResult(-1, timedOut, lines.ToArray());
        }

        p.WaitForExit(); // garante o término da leitura assíncrona da saída
        lock (gate) return new ProcessResult(p.ExitCode, false, lines.ToArray());
    }
}
