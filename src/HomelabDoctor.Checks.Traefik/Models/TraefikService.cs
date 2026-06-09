namespace HomelabDoctor.Checks.Traefik.Models;

public sealed class TraefikService
{
    public required string Name { get; init; }
    public required string Status { get; init; }
    public required IReadOnlyList<string> UsedBy { get; init; }
    public required IReadOnlyList<string> ServerUrls { get; init; }
    public required IReadOnlyDictionary<string, string> ServerStatus { get; init; }

    public int TotalServerCount => ServerStatus.Count;
    public int HealthyServerCount => ServerStatus.Count(s => s.Value.Equals("UP", StringComparison.OrdinalIgnoreCase));
}
