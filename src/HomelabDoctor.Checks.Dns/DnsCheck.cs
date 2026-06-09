using HomelabDoctor.Checks.Dns.Config;
using HomelabDoctor.Core.Interfaces;
using HomelabDoctor.Core.Models;

namespace HomelabDoctor.Checks.Dns;

public sealed class DnsCheck : ICheck
{
    public string Name => "DNS";

    private static readonly string[] PublicTestHostnames = ["github.com", "cloudflare.com"];
    private const string PublicDnsServer = "1.1.1.1";

    private readonly IDnsInspector _inspector;
    private readonly HomelabDoctorConfig? _config;

    public DnsCheck(IDnsInspector inspector, HomelabDoctorConfig? config = null)
    {
        _inspector = inspector;
        _config = config;
    }

    public async Task<CheckResult> RunAsync(CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
        var findings = new List<Finding>();

        // 1. Discover configured nameservers
        var nameservers = await _inspector.GetConfiguredNameserversAsync(ct);

        if (nameservers.Count == 0)
        {
            findings.Add(new Finding
            {
                Id = "DNS_NO_SERVERS",
                Title = "No DNS servers found in /etc/resolv.conf",
                Severity = Severity.Critical,
                Category = "Configuration",
                Evidence = "/etc/resolv.conf has no nameserver entries",
                Explanation = "Without a configured DNS server, hostname resolution will fail. This is unusual and likely indicates a misconfigured network or container environment.",
                SuggestedFix = "Check /etc/resolv.conf and ensure at least one nameserver is configured.",
                SuggestedCommands = ["cat /etc/resolv.conf", "systemd-resolve --status"]
            });
        }
        else
        {
            findings.Add(new Finding
            {
                Id = "DNS_SERVERS",
                Title = $"DNS server(s) configured: {string.Join(", ", nameservers)}",
                Severity = Severity.Info,
                Category = "Configuration",
                Evidence = string.Join(", ", nameservers),
                Explanation = "These are the nameservers configured in /etc/resolv.conf."
            });
        }

        // 2. Test resolution of public hostnames via local DNS
        var localResolutionFailed = false;
        foreach (var hostname in PublicTestHostnames)
        {
            var result = await _inspector.ResolveLocalAsync(hostname, ct);
            if (!result.Resolved)
            {
                localResolutionFailed = true;
                findings.Add(new Finding
                {
                    Id = $"DNS_LOCAL_NXDOMAIN_{hostname.Replace(".", "_").ToUpperInvariant()}",
                    Title = $"Cannot resolve {hostname} via local DNS",
                    Severity = Severity.Critical,
                    Category = "Resolution",
                    Evidence = result.Error ?? "NXDOMAIN or no response",
                    Explanation = $"The local DNS server could not resolve {hostname}, a well-known public domain. This usually means local DNS is broken or not reachable — which would affect all hostname resolution on this machine.",
                    SuggestedFix = $"Check that your DNS server is running and reachable. Try: dig {hostname} and compare with: dig @1.1.1.1 {hostname}",
                    SuggestedCommands = [$"dig {hostname}", $"dig @{PublicDnsServer} {hostname}", "cat /etc/resolv.conf"]
                });
            }
            else
            {
                findings.Add(new Finding
                {
                    Id = $"DNS_LOCAL_OK_{hostname.Replace(".", "_").ToUpperInvariant()}",
                    Title = $"{hostname} resolves via local DNS",
                    Severity = Severity.Info,
                    Category = "Resolution",
                    Evidence = string.Join(", ", result.Addresses),
                    Explanation = "Local DNS can resolve this public hostname."
                });
            }
        }

        // 3. Public DNS comparison (requires dig)
        var digAvailable = await _inspector.IsDigAvailableAsync(ct);

        if (!digAvailable)
        {
            findings.Add(new Finding
            {
                Id = "DNS_DIG_UNAVAILABLE",
                Title = "dig is not available — skipping public DNS comparison",
                Severity = Severity.Info,
                Category = "Tooling",
                Evidence = "dig not found on PATH",
                Explanation = "Public DNS comparison requires dig (part of dnsutils / bind-utils). Install it to enable local-vs-public DNS checks.",
                SuggestedCommands = ["apt-get install dnsutils", "# or: yum install bind-utils"]
            });
        }
        else if (!localResolutionFailed)
        {
            foreach (var hostname in PublicTestHostnames)
            {
                var localResult = await _inspector.ResolveLocalAsync(hostname, ct);
                var publicResult = await _inspector.ResolveViaServerAsync(hostname, PublicDnsServer, ct);

                if (!publicResult.Resolved) continue;

                // Check if local DNS is returning a blocked/sinkhole IP
                var localBlockIndicators = localResult.Addresses
                    .Where(a => a is "0.0.0.0" or "127.0.0.1" or "::1")
                    .ToList();

                if (localBlockIndicators.Count > 0)
                {
                    findings.Add(new Finding
                    {
                        Id = $"DNS_BLOCKED_{hostname.Replace(".", "_").ToUpperInvariant()}",
                        Title = $"{hostname} appears to be blocked or sinkholed by local DNS",
                        Severity = Severity.Warning,
                        Category = "Split-Horizon",
                        Evidence = $"Local DNS returned: {string.Join(", ", localResult.Addresses)}. Public DNS (1.1.1.1) returned: {string.Join(", ", publicResult.Addresses)}",
                        Explanation = "Your local DNS server (likely Pi-hole or AdGuard Home) is returning a sinkhole address for this domain. This is usually intentional for ad/tracker blocking but can cause unexpected failures if a legitimate service uses this domain.",
                        SuggestedFix = "If this is unintentional, add an exception/whitelist entry in your local DNS resolver."
                    });
                }
                else
                {
                    // Check for meaningful split-horizon (different real IPs)
                    var localSet = localResult.Addresses.ToHashSet();
                    var publicSet = publicResult.Addresses.ToHashSet();
                    if (!localSet.Overlaps(publicSet))
                    {
                        findings.Add(new Finding
                        {
                            Id = $"DNS_SPLIT_HORIZON_{hostname.Replace(".", "_").ToUpperInvariant()}",
                            Title = $"Split-horizon DNS detected for {hostname}",
                            Severity = Severity.Info,
                            Category = "Split-Horizon",
                            Evidence = $"Local: {string.Join(", ", localResult.Addresses)}. Public (1.1.1.1): {string.Join(", ", publicResult.Addresses)}",
                            Explanation = "Local and public DNS return different addresses for this hostname. This is sometimes intentional (e.g., returning a LAN IP instead of a public IP for local services), but can cause connectivity issues for devices that bypass your local DNS."
                        });
                    }
                }
            }
        }

        // 4. Config-driven expected record checks
        var expectedRecords = _config?.Dns?.Records ?? [];
        foreach (var record in expectedRecords)
        {
            var result = await _inspector.ResolveLocalAsync(record.Hostname, ct);
            if (!result.Resolved)
            {
                findings.Add(new Finding
                {
                    Id = $"DNS_EXPECTED_NXDOMAIN_{record.Hostname.Replace(".", "_").ToUpperInvariant()}",
                    Title = $"Expected hostname {record.Hostname} does not resolve",
                    Severity = Severity.Critical,
                    Category = "Expected Records",
                    Evidence = $"Expected: {record.Expected}. Actual: NXDOMAIN / not found",
                    Explanation = $"Your homelab-doctor.yml expects {record.Hostname} to resolve to {record.Expected}, but local DNS returned nothing. The hostname may not be registered in your local DNS server.",
                    SuggestedFix = $"Add an A record for {record.Hostname} pointing to {record.Expected} in your local DNS server.",
                    SuggestedCommands = [$"dig {record.Hostname}"]
                });
            }
            else if (!result.Addresses.Contains(record.Expected))
            {
                findings.Add(new Finding
                {
                    Id = $"DNS_EXPECTED_MISMATCH_{record.Hostname.Replace(".", "_").ToUpperInvariant()}",
                    Title = $"{record.Hostname} resolves to wrong address",
                    Severity = Severity.Warning,
                    Category = "Expected Records",
                    Evidence = $"Expected: {record.Expected}. Actual: {string.Join(", ", result.Addresses)}",
                    Explanation = $"Your homelab-doctor.yml expects {record.Hostname} to resolve to {record.Expected}, but the actual address differs. This may mean DNS records are stale or pointing to an old IP.",
                    SuggestedFix = $"Update the A record for {record.Hostname} in your local DNS server to {record.Expected}.",
                    SuggestedCommands = [$"dig {record.Hostname}"]
                });
            }
            else
            {
                findings.Add(new Finding
                {
                    Id = $"DNS_EXPECTED_OK_{record.Hostname.Replace(".", "_").ToUpperInvariant()}",
                    Title = $"{record.Hostname} resolves correctly",
                    Severity = Severity.Info,
                    Category = "Expected Records",
                    Evidence = $"Resolved to {string.Join(", ", result.Addresses)} (expected {record.Expected})",
                    Explanation = "This hostname matches the expected record in homelab-doctor.yml."
                });
            }
        }

        var worstSeverity = findings
            .Where(f => f.Id != "DNS_SERVERS" && !f.Id.StartsWith("DNS_LOCAL_OK_") && !f.Id.StartsWith("DNS_EXPECTED_OK_"))
            .Select(f => f.Severity)
            .DefaultIfEmpty(Severity.Info)
            .Max();

        var status = worstSeverity switch
        {
            Severity.Critical => CheckStatus.Critical,
            Severity.Warning => CheckStatus.Warning,
            _ => CheckStatus.Passed
        };

        var criticalCount = findings.Count(f => f.Severity == Severity.Critical);
        var warningCount = findings.Count(f => f.Severity == Severity.Warning);
        var summary = (criticalCount, warningCount) switch
        {
            (0, 0) => "DNS looks healthy. Local resolution is working.",
            (var c, 0) => $"{c} critical DNS issue(s) found.",
            (0, var w) => $"{w} DNS warning(s) found.",
            (var c, var w) => $"{c} critical issue(s) and {w} warning(s) found."
        };

        return new CheckResult
        {
            CheckName = Name,
            Status = status,
            Findings = findings.OrderByDescending(f => f.Severity).ToList(),
            Summary = summary,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow
        };
    }
}
