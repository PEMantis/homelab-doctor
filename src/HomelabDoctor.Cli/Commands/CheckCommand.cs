using System.CommandLine;
using HomelabDoctor.Checks.Docker;
using HomelabDoctor.Checks.Dns;
using HomelabDoctor.Checks.Dns.Config;
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
        dockerCommand.SetAction(async (_, ct) =>
        {
            var result = await RunDockerCheck(ct);
            new SpectreConsoleRenderer().Render([result]);
        });

        var dnsCommand = new Command("dns", "Inspect local DNS resolution and configuration");
        dnsCommand.SetAction(async (_, ct) =>
        {
            var result = await RunDnsCheck(ct);
            new SpectreConsoleRenderer().Render([result]);
        });

        // Default: run all checks
        checkCommand.SetAction(async (_, ct) =>
        {
            CheckResult docker = null!;
            CheckResult dns = null!;

            await AnsiConsole.Status()
                .StartAsync("Running all checks...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.Status("Running Docker check...");
                    docker = await new DockerCheck(new DockerInspector(new CommandRunner())).RunAsync(ct);
                    ctx.Status("Running DNS check...");
                    dns = await new DnsCheck(new DnsInspector(new CommandRunner()), ConfigLoader.TryLoad()).RunAsync(ct);
                });

            new SpectreConsoleRenderer().Render([docker, dns]);
        });

        checkCommand.Add(dockerCommand);
        checkCommand.Add(dnsCommand);
        return checkCommand;
    }

    internal static async Task<CheckResult> RunDockerCheck(CancellationToken ct)
    {
        var runner = new CommandRunner();
        CheckResult result = null!;
        await AnsiConsole.Status()
            .StartAsync("Running Docker check...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                result = await new DockerCheck(new DockerInspector(runner)).RunAsync(ct);
            });
        return result;
    }

    internal static async Task<CheckResult> RunDnsCheck(CancellationToken ct)
    {
        var runner = new CommandRunner();
        CheckResult result = null!;
        await AnsiConsole.Status()
            .StartAsync("Running DNS check...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                result = await new DnsCheck(new DnsInspector(runner), ConfigLoader.TryLoad()).RunAsync(ct);
            });
        return result;
    }
}
