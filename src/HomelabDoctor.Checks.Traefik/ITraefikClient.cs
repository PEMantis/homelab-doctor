using HomelabDoctor.Checks.Traefik.Models;

namespace HomelabDoctor.Checks.Traefik;

public interface ITraefikClient
{
    Task<TraefikAvailability> GetAvailabilityAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TraefikRouter>> GetHttpRoutersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TraefikService>> GetHttpServicesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TraefikMiddleware>> GetHttpMiddlewaresAsync(CancellationToken ct = default);
}
