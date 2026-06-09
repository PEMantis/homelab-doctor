namespace HomelabDoctor.Checks.Docker.Models;

public sealed class ContainerSummary
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Image { get; init; }
    public required string State { get; init; }
    public required string Status { get; init; }
    public required int RestartCount { get; init; }
    public required bool HasHealthcheck { get; init; }
    public required string? HealthStatus { get; init; }
    public required IReadOnlyList<string> PublicPorts { get; init; }
}
