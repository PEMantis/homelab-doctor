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

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new CommandOutput(-1, string.Empty, $"executable not found: {executable}");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);

        return new CommandOutput(process.ExitCode, await stdoutTask, await stderrTask);
    }
}
