namespace HomelabDoctor.Checks.Traefik.Models;

public sealed class TraefikRouter
{
    public required string Name { get; init; }
    public required string Rule { get; init; }
    public required string Status { get; init; }
    public required IReadOnlyList<string> EntryPoints { get; init; }
    public required string Service { get; init; }
    public required IReadOnlyList<string> Middlewares { get; init; }
    public required bool HasTls { get; init; }
    public string? TlsCertResolver { get; init; }
    public required IReadOnlyList<string> Hostnames { get; init; }
}
