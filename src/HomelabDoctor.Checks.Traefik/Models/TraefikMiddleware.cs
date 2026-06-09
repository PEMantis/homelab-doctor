namespace HomelabDoctor.Checks.Traefik.Models;

public sealed class TraefikMiddleware
{
    public required string Name { get; init; }
    public required string Status { get; init; }
    public required IReadOnlyList<string> UsedBy { get; init; }
    public required string Type { get; init; }
}
