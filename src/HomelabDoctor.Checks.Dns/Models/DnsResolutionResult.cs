namespace HomelabDoctor.Checks.Dns.Models;

public sealed class DnsResolutionResult
{
    public required string Hostname { get; init; }
    public required bool Resolved { get; init; }
    public IReadOnlyList<string> Addresses { get; init; } = [];
    public string? Error { get; init; }
    public string? QueryServer { get; init; }
}
