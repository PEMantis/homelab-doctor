using HomelabDoctor.Core.Interfaces;
using HomelabDoctor.Core.Models;
using Spectre.Console;

namespace HomelabDoctor.Reporting;

public sealed class SpectreConsoleRenderer : IReportRenderer
{
    public void Render(IReadOnlyList<CheckResult> results)
    {
        AnsiConsole.Write(new FigletText("Homelab Doctor").Color(Color.DodgerBlue1));
        AnsiConsole.MarkupLine($"[grey]Ran at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC[/]");
        AnsiConsole.WriteLine();

        foreach (var result in results)
            RenderResult(result);
    }

    public string RenderToString(IReadOnlyList<CheckResult> results)
    {
        // Console renderer writes directly; call the markdown renderer for string output
        throw new NotSupportedException("Use MarkdownRenderer.RenderToString for text output.");
    }

    private static void RenderResult(CheckResult result)
    {
        var statusIcon = result.Status switch
        {
            CheckStatus.Passed => "[green]✓[/]",
            CheckStatus.Warning => "[yellow]⚠[/]",
            CheckStatus.Critical => "[red]✗[/]",
            CheckStatus.Unavailable => "[grey]○[/]",
            _ => "[grey]?[/]"
        };

        AnsiConsole.MarkupLine($"{statusIcon} [bold]{Markup.Escape(result.CheckName)} Check[/]");
        AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(result.Summary)}[/]");
        AnsiConsole.WriteLine();

        if (result.Status == CheckStatus.Unavailable)
        {
            foreach (var f in result.Findings)
                AnsiConsole.MarkupLine($"  [yellow]→[/] {Markup.Escape(f.Evidence)}");
            AnsiConsole.WriteLine();
            return;
        }

        // Summary status line per finding
        foreach (var f in result.Findings)
        {
            var icon = f.Severity switch
            {
                Severity.Critical => "[red]✗[/]",
                Severity.Warning => "[yellow]⚠[/]",
                _ => "[green]✓[/]"
            };
            AnsiConsole.MarkupLine($"  {icon}  {Markup.Escape(f.Title)}");
        }
        AnsiConsole.WriteLine();

        // Detailed table for non-info findings
        var actionable = result.Findings.Where(f => f.Severity >= Severity.Warning).ToList();
        if (actionable.Count == 0) return;

        AnsiConsole.MarkupLine("[bold]Findings[/]");
        AnsiConsole.WriteLine();

        foreach (var f in actionable)
        {
            var severityStyle = f.Severity == Severity.Critical ? "red bold" : "yellow bold";
            var severityLabel = f.Severity.ToString().ToUpperInvariant();

            AnsiConsole.MarkupLine($"[{severityStyle}][{severityLabel}][/] {Markup.Escape(f.Title)}");

            if (!string.IsNullOrEmpty(f.Evidence))
                AnsiConsole.MarkupLine($"  [grey]Evidence:[/] {Markup.Escape(f.Evidence)}");

            if (!string.IsNullOrEmpty(f.Explanation))
                AnsiConsole.MarkupLine($"  [grey]Why it matters:[/] {Markup.Escape(f.Explanation)}");

            if (!string.IsNullOrEmpty(f.SuggestedFix))
                AnsiConsole.MarkupLine($"  [grey]Fix:[/] {Markup.Escape(f.SuggestedFix)}");

            if (f.SuggestedCommands.Count > 0)
            {
                AnsiConsole.MarkupLine("  [grey]Try:[/]");
                foreach (var cmd in f.SuggestedCommands)
                    AnsiConsole.MarkupLine($"    [dim]$[/] [cyan]{Markup.Escape(cmd)}[/]");
            }

            if (!string.IsNullOrEmpty(f.DocumentationUrl))
                AnsiConsole.MarkupLine($"  [grey]Docs:[/] [link]{Markup.Escape(f.DocumentationUrl)}[/]");

            AnsiConsole.WriteLine();
        }
    }
}
