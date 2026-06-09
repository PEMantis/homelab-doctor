namespace HomelabDoctor.Core.Interfaces;

public record CommandOutput(int ExitCode, string Stdout, string Stderr);

public interface ICommandRunner
{
    Task<CommandOutput> RunAsync(string executable, string arguments, CancellationToken ct = default);
}
