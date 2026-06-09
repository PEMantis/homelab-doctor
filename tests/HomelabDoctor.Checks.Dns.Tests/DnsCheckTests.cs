using HomelabDoctor.Checks.Dns;
using HomelabDoctor.Checks.Dns.Config;
using HomelabDoctor.Checks.Dns.Models;
using HomelabDoctor.Core.Models;
using NSubstitute;
using Xunit;

namespace HomelabDoctor.Checks.Dns.Tests;

public class DnsCheckTests
{
    private static IDnsInspector MakeInspector(
        IReadOnlyList<string>? nameservers = null,
        bool digAvailable = false)
    {
        var inspector = Substitute.For<IDnsInspector>();
        inspector.GetConfiguredNameserversAsync(default)
            .ReturnsForAnyArgs(nameservers ?? ["192.168.1.1"]);
        inspector.IsDigAvailableAsync(default)
            .ReturnsForAnyArgs(digAvailable);
        // Default: resolve any hostname successfully
        inspector.ResolveLocalAsync(Arg.Any<string>(), default)
            .ReturnsForAnyArgs(ci => new DnsResolutionResult
            {
                Hostname = ci.Arg<string>(),
                Resolved = true,
                Addresses = ["1.2.3.4"]
            });
        return inspector;
    }

    [Fact]
    public async Task Returns_critical_when_no_nameservers_configured()
    {
        var inspector = MakeInspector(nameservers: []);
        var check = new DnsCheck(inspector);

        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Critical, result.Status);
        Assert.Contains(result.Findings, f => f.Id == "DNS_NO_SERVERS");
    }

    [Fact]
    public async Task Returns_info_finding_listing_nameservers()
    {
        var inspector = MakeInspector(nameservers: ["192.168.1.1", "192.168.1.2"]);
        var check = new DnsCheck(inspector);

        var result = await check.RunAsync();

        var f = result.Findings.SingleOrDefault(f => f.Id == "DNS_SERVERS");
        Assert.NotNull(f);
        Assert.Contains("192.168.1.1", f.Evidence);
        Assert.Contains("192.168.1.2", f.Evidence);
    }

    [Fact]
    public async Task Returns_critical_when_public_hostname_does_not_resolve()
    {
        var inspector = MakeInspector();
        inspector.ResolveLocalAsync("github.com", default)
            .ReturnsForAnyArgs(new DnsResolutionResult { Hostname = "github.com", Resolved = false, Error = "NXDOMAIN" });

        var check = new DnsCheck(inspector);
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Critical, result.Status);
        Assert.Contains(result.Findings, f => f.Id == "DNS_LOCAL_NXDOMAIN_GITHUB_COM");
        Assert.Contains(result.Findings.First(f => f.Id == "DNS_LOCAL_NXDOMAIN_GITHUB_COM").SuggestedCommands,
            c => c.Contains("dig"));
    }

    [Fact]
    public async Task Returns_passed_when_local_dns_works_and_no_config()
    {
        var inspector = MakeInspector(digAvailable: false);
        var check = new DnsCheck(inspector);

        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Passed, result.Status);
    }

    [Fact]
    public async Task Produces_warning_when_domain_is_sinkholed_by_local_dns()
    {
        var inspector = MakeInspector(digAvailable: true);
        inspector.ResolveLocalAsync(Arg.Is<string>(h => h == "github.com"), Arg.Any<CancellationToken>())
            .Returns(new DnsResolutionResult { Hostname = "github.com", Resolved = true, Addresses = ["0.0.0.0"] });
        inspector.ResolveLocalAsync(Arg.Is<string>(h => h == "cloudflare.com"), Arg.Any<CancellationToken>())
            .Returns(new DnsResolutionResult { Hostname = "cloudflare.com", Resolved = true, Addresses = ["1.1.1.1"] });
        inspector.ResolveViaServerAsync(Arg.Is<string>(h => h == "github.com"), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DnsResolutionResult { Hostname = "github.com", Resolved = true, Addresses = ["140.82.121.4"], QueryServer = "1.1.1.1" });
        inspector.ResolveViaServerAsync(Arg.Is<string>(h => h == "cloudflare.com"), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DnsResolutionResult { Hostname = "cloudflare.com", Resolved = true, Addresses = ["1.1.1.1"], QueryServer = "1.1.1.1" });

        var check = new DnsCheck(inspector);
        var result = await check.RunAsync();

        var finding = result.Findings.SingleOrDefault(f => f.Id == "DNS_BLOCKED_GITHUB_COM");
        Assert.NotNull(finding);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("0.0.0.0", finding.Evidence);
    }

    [Fact]
    public async Task Produces_info_finding_for_split_horizon_with_different_real_ips()
    {
        var inspector = MakeInspector(digAvailable: true);
        inspector.ResolveLocalAsync(Arg.Is<string>(h => h == "github.com"), Arg.Any<CancellationToken>())
            .Returns(new DnsResolutionResult { Hostname = "github.com", Resolved = true, Addresses = ["192.168.1.50"] });
        inspector.ResolveLocalAsync(Arg.Is<string>(h => h == "cloudflare.com"), Arg.Any<CancellationToken>())
            .Returns(new DnsResolutionResult { Hostname = "cloudflare.com", Resolved = true, Addresses = ["1.1.1.1"] });
        inspector.ResolveViaServerAsync(Arg.Is<string>(h => h == "github.com"), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DnsResolutionResult { Hostname = "github.com", Resolved = true, Addresses = ["140.82.121.4"], QueryServer = "1.1.1.1" });
        inspector.ResolveViaServerAsync(Arg.Is<string>(h => h == "cloudflare.com"), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DnsResolutionResult { Hostname = "cloudflare.com", Resolved = true, Addresses = ["1.1.1.1"], QueryServer = "1.1.1.1" });

        var check = new DnsCheck(inspector);
        var result = await check.RunAsync();

        var finding = result.Findings.SingleOrDefault(f => f.Id == "DNS_SPLIT_HORIZON_GITHUB_COM");
        Assert.NotNull(finding);
        Assert.Equal(Severity.Info, finding.Severity);
    }

    [Fact]
    public async Task Produces_critical_finding_when_expected_record_does_not_resolve()
    {
        var inspector = MakeInspector();
        inspector.ResolveLocalAsync("grafana.home.arpa", default)
            .ReturnsForAnyArgs(new DnsResolutionResult { Hostname = "grafana.home.arpa", Resolved = false, Error = "NXDOMAIN" });

        var config = new HomelabDoctorConfig
        {
            Dns = new DnsConfig
            {
                Records = [new ExpectedDnsRecord { Hostname = "grafana.home.arpa", Expected = "192.168.1.50" }]
            }
        };

        var check = new DnsCheck(inspector, config);
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Critical, result.Status);
        Assert.Contains(result.Findings, f => f.Id == "DNS_EXPECTED_NXDOMAIN_GRAFANA_HOME_ARPA");
    }

    [Fact]
    public async Task Produces_warning_when_expected_record_resolves_to_wrong_ip()
    {
        var inspector = MakeInspector();
        inspector.ResolveLocalAsync("traefik.home.arpa", default)
            .ReturnsForAnyArgs(new DnsResolutionResult { Hostname = "traefik.home.arpa", Resolved = true, Addresses = ["192.168.1.99"] });

        var config = new HomelabDoctorConfig
        {
            Dns = new DnsConfig
            {
                Records = [new ExpectedDnsRecord { Hostname = "traefik.home.arpa", Expected = "192.168.1.50" }]
            }
        };

        var check = new DnsCheck(inspector, config);
        var result = await check.RunAsync();

        var finding = result.Findings.SingleOrDefault(f => f.Id == "DNS_EXPECTED_MISMATCH_TRAEFIK_HOME_ARPA");
        Assert.NotNull(finding);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("192.168.1.99", finding.Evidence);
        Assert.Contains("192.168.1.50", finding.Evidence);
    }

    [Fact]
    public async Task Produces_info_finding_when_expected_record_is_correct()
    {
        var inspector = MakeInspector();
        inspector.ResolveLocalAsync("pihole.home.arpa", default)
            .ReturnsForAnyArgs(new DnsResolutionResult { Hostname = "pihole.home.arpa", Resolved = true, Addresses = ["192.168.1.2"] });

        var config = new HomelabDoctorConfig
        {
            Dns = new DnsConfig
            {
                Records = [new ExpectedDnsRecord { Hostname = "pihole.home.arpa", Expected = "192.168.1.2" }]
            }
        };

        var check = new DnsCheck(inspector, config);
        var result = await check.RunAsync();

        Assert.Contains(result.Findings, f => f.Id == "DNS_EXPECTED_OK_PIHOLE_HOME_ARPA" && f.Severity == Severity.Info);
    }
}
