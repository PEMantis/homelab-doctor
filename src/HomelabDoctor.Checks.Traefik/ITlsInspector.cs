using HomelabDoctor.Checks.Traefik.Models;

namespace HomelabDoctor.Checks.Traefik;

public interface ITlsInspector
{
    Task<TlsCertInfo> CheckAsync(string hostname, int port = 443, CancellationToken ct = default);
}
