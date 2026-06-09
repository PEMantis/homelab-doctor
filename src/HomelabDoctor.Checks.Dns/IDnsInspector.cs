using HomelabDoctor.Checks.Dns.Models;

namespace HomelabDoctor.Checks.Dns;

public interface IDnsInspector
{
    Task<IReadOnlyList<string>> GetConfiguredNameserversAsync(CancellationToken ct = default);
    Task<DnsResolutionResult> ResolveLocalAsync(string hostname, CancellationToken ct = default);
    Task<DnsResolutionResult> ResolveViaServerAsync(string hostname, string server, CancellationToken ct = default);
    Task<bool> IsDigAvailableAsync(CancellationToken ct = default);
}
