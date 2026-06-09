namespace HomelabDoctor.Core.Models;

public sealed class CheckResult
{
    public required string CheckName { get; init; }
    public required CheckStatus Status { get; init; }
    public required IReadOnlyList<Finding> Findings { get; init; }
    public required string Summary { get; init; }
    public required DateTime StartedAt { get; init; }
    public required DateTime CompletedAt { get; init; }

    public static CheckResult Unavailable(string checkName, string reason, DateTime startedAt) =>
        new()
        {
            CheckName = checkName,
            Status = CheckStatus.Unavailable,
            Findings =
            [
                new Finding
                {
                    Id = "UNAVAILABLE",
                    Title = $"{checkName} is unavailable",
                    Severity = Severity.Warning,
                    Category = "Availability",
                    Evidence = reason,
                    Explanation = reason
                }
            ],
            Summary = reason,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow
        };
}
