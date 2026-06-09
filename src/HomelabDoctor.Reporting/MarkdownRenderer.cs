using System.Text;
using HomelabDoctor.Core.Interfaces;
using HomelabDoctor.Core.Models;

namespace HomelabDoctor.Reporting;

public sealed class MarkdownRenderer : IReportRenderer
{
    public void Render(IReadOnlyList<CheckResult> results)
    {
        Console.Write(RenderToString(results));
    }

    public string RenderToString(IReadOnlyList<CheckResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Homelab Doctor Report");
        sb.AppendLine();
        sb.AppendLine($"_Generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC_");
        sb.AppendLine();

        foreach (var result in results)
            AppendResult(sb, result);

        return sb.ToString();
    }

    private static void AppendResult(StringBuilder sb, CheckResult result)
    {
        var statusBadge = result.Status switch
        {
            CheckStatus.Passed => "✅ Passed",
            CheckStatus.Warning => "⚠️ Warning",
            CheckStatus.Critical => "❌ Critical",
            CheckStatus.Unavailable => "⚪ Unavailable",
            _ => "❓ Unknown"
        };

        sb.AppendLine($"## {result.CheckName} — {statusBadge}");
        sb.AppendLine();
        sb.AppendLine($"> {result.Summary}");
        sb.AppendLine();
        sb.AppendLine($"_Duration: {(result.CompletedAt - result.StartedAt).TotalMilliseconds:F0}ms_");
        sb.AppendLine();

        if (result.Status == CheckStatus.Unavailable)
        {
            foreach (var f in result.Findings)
                sb.AppendLine($"**{f.Evidence}**");
            sb.AppendLine();
            return;
        }

        var actionable = result.Findings.Where(f => f.Severity >= Severity.Warning).ToList();
        if (actionable.Count == 0)
        {
            sb.AppendLine("No warnings or critical findings.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("### Findings");
        sb.AppendLine();

        foreach (var f in actionable)
        {
            var severityEmoji = f.Severity == Severity.Critical ? "❌" : "⚠️";
            sb.AppendLine($"#### {severityEmoji} {f.Title}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(f.Evidence))
                sb.AppendLine($"**Evidence:** {f.Evidence}");

            if (!string.IsNullOrEmpty(f.Explanation))
                sb.AppendLine($"**Why it matters:** {f.Explanation}");

            if (!string.IsNullOrEmpty(f.SuggestedFix))
                sb.AppendLine($"**Suggested fix:** {f.SuggestedFix}");

            if (f.SuggestedCommands.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("**Try:**");
                sb.AppendLine("```bash");
                foreach (var cmd in f.SuggestedCommands)
                    sb.AppendLine(cmd);
                sb.AppendLine("```");
            }

            if (!string.IsNullOrEmpty(f.DocumentationUrl))
                sb.AppendLine($"**Docs:** {f.DocumentationUrl}");

            sb.AppendLine();
        }
    }
}
