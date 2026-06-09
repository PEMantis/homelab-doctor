using System.CommandLine;
using HomelabDoctor.Checks.Docker;
using HomelabDoctor.Core.Models;
using HomelabDoctor.Core.Runner;
using HomelabDoctor.Reporting;
using Spectre.Console;

namespace HomelabDoctor.Cli.Commands;

public static class CheckCommand
{
    public static Command Build()
    {
        var checkCommand = new Command("check", "Run diagnostic checks");

        var dockerCommand = new Command("docker", "Inspect the local Docker environment");
        dockerCommand.SetAction(async (parseResult, ct) =>
        {
            var result = await RunDockerCheck(ct);
            new SpectreConsoleRenderer().Render([result]);
        });

        // Default: run docker check when no subcommand is given
        checkCommand.SetAction(async (parseResult, ct) =>
        {
            var result = await RunDockerCheck(ct);
            new SpectreConsoleRenderer().Render([result]);
        });

        checkCommand.Add(dockerCommand);
        return checkCommand;
    }

    private static async Task<CheckResult> RunDockerCheck(CancellationToken ct)
    {
        var runner = new CommandRunner();
        var inspector = new DockerInspector(runner);
        var check = new DockerCheck(inspector);

        CheckResult result = null!;
        await AnsiConsole.Status()
            .StartAsync("Running Docker check...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                result = await check.RunAsync(ct);
            });
        return result;
    }
}
