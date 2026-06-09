using System.Diagnostics;
using HomelabDoctor.Core.Interfaces;

namespace HomelabDoctor.Core.Runner;

public sealed class CommandRunner : ICommandRunner
{
    public async Task<CommandOutput> RunAsync(string executable, string arguments, CancellationToken ct = default)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);

        return new CommandOutput(process.ExitCode, await stdoutTask, await stderrTask);
    }
}
