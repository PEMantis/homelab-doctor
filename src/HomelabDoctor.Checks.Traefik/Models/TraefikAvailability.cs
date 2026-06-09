namespace HomelabDoctor.Checks.Traefik.Models;

public enum TraefikAvailabilityStatus
{
    Available,
    Unavailable,
    Unauthorized
}

public sealed class TraefikAvailability
{
    public required TraefikAvailabilityStatus Status { get; init; }
    public string? Message { get; init; }
    public string? ApiUrl { get; init; }
}
