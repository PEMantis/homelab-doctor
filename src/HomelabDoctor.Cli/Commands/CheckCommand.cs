using System.CommandLine;
using HomelabDoctor.Checks.Docker;
using HomelabDoctor.Checks.Dns;
using HomelabDoctor.Checks.Dns.Config;
using HomelabDoctor.Checks.Traefik;
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
            new SpectreConsoleRenderer().Render([await RunDockerCheck(ct)]));

        var dnsCommand = new Command("dns", "Inspect local DNS resolution and configuration");
        dnsCommand.SetAction(async (_, ct) =>
            new SpectreConsoleRenderer().Render([await RunDnsCheck(ct)]));

        var traefikCommand = new Command("traefik", "Inspect the Traefik reverse proxy");
        traefikCommand.SetAction(async (_, ct) =>
            new SpectreConsoleRenderer().Render([await RunTraefikCheck(ct)]));

        // Default: run all checks
        checkCommand.SetAction(async (_, ct) =>
        {
            CheckResult docker = null!, dns = null!, traefik = null!;
            await AnsiConsole.Status()
                .StartAsync("Running all checks...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.Status("Running Docker check...");
                    docker = await new DockerCheck(new DockerInspector(new CommandRunner())).RunAsync(ct);
                    ctx.Status("Running DNS check...");
                    dns = await new DnsCheck(new DnsInspector(new CommandRunner()), ConfigLoader.TryLoad()).RunAsync(ct);
                    ctx.Status("Running Traefik check...");
                    traefik = await new TraefikCheck(new TraefikClient(), new TlsInspector()).RunAsync(ct);
                });
            new SpectreConsoleRenderer().Render([docker, dns, traefik]);
        });

        checkCommand.Add(dockerCommand);
        checkCommand.Add(dnsCommand);
        checkCommand.Add(traefikCommand);
        return checkCommand;
    }

    internal static async Task<CheckResult> RunDockerCheck(CancellationToken ct)
    {
        CheckResult result = null!;
        await AnsiConsole.Status().StartAsync("Running Docker check...", async ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);
            result = await new DockerCheck(new DockerInspector(new CommandRunner())).RunAsync(ct);
        });
        return result;
    }

    internal static async Task<CheckResult> RunDnsCheck(CancellationToken ct)
    {
        CheckResult result = null!;
        await AnsiConsole.Status().StartAsync("Running DNS check...", async ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);
            result = await new DnsCheck(new DnsInspector(new CommandRunner()), ConfigLoader.TryLoad()).RunAsync(ct);
        });
        return result;
    }

    internal static async Task<CheckResult> RunTraefikCheck(CancellationToken ct)
    {
        CheckResult result = null!;
        await AnsiConsole.Status().StartAsync("Running Traefik check...", async ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);
            result = await new TraefikCheck(new TraefikClient(), new TlsInspector()).RunAsync(ct);
        });
        return result;
    }
}
