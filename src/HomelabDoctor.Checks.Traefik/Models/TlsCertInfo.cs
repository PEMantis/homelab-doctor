namespace HomelabDoctor.Checks.Traefik.Models;

public sealed class TlsCertInfo
{
    public required string Hostname { get; init; }
    public required bool Connected { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? Error { get; init; }

    public int? DaysUntilExpiry => ExpiresAt.HasValue
        ? (int)(ExpiresAt.Value.ToUniversalTime() - DateTime.UtcNow).TotalDays
        : null;
}
