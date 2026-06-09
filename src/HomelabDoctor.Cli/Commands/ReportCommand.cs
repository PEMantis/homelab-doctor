using System.CommandLine;
using HomelabDoctor.Core.Models;
using HomelabDoctor.Reporting;
using Spectre.Console;

namespace HomelabDoctor.Cli.Commands;

public static class ReportCommand
{
    public static Command Build()
    {
        var formatOption = new Option<string>("--format", ["-f"])
        {
            Description = "Output format: markdown (default) or console",
            DefaultValueFactory = _ => "markdown"
        };

        var outputOption = new Option<string?>("--output", ["-o"])
        {
            Description = "Write report to a file instead of stdout"
        };

        var reportCommand = new Command("report", "Generate a diagnostic report");
        reportCommand.Add(formatOption);
        reportCommand.Add(outputOption);

        reportCommand.SetAction(async (parseResult, ct) =>
        {
            var format = parseResult.GetValue(formatOption) ?? "markdown";
            var output = parseResult.GetValue(outputOption);

            var docker = await CheckCommand.RunDockerCheck(ct);
            var dns = await CheckCommand.RunDnsCheck(ct);
            IReadOnlyList<CheckResult> results = [docker, dns];

            if (format == "console")
            {
                new SpectreConsoleRenderer().Render(results);
                return;
            }

            var markdown = new MarkdownRenderer().RenderToString(results);

            if (output is not null)
            {
                await File.WriteAllTextAsync(output, markdown, ct);
                AnsiConsole.MarkupLine($"[green]Report written to {Markup.Escape(output)}[/]");
            }
            else
            {
                Console.Write(markdown);
            }
        });

        return reportCommand;
    }
}
