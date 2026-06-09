using HomelabDoctor.Checks.Traefik;
using HomelabDoctor.Checks.Traefik.Models;
using HomelabDoctor.Core.Models;
using NSubstitute;
using Xunit;

namespace HomelabDoctor.Checks.Traefik.Tests;

public class TraefikCheckTests
{
    private static TraefikAvailability Available() => new()
    {
        Status = TraefikAvailabilityStatus.Available,
        ApiUrl = "http://localhost:8080"
    };

    private static ITlsInspector NoOpTls()
    {
        var tls = Substitute.For<ITlsInspector>();
        tls.CheckAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => new TlsCertInfo { Hostname = ci.Arg<string>(), Connected = false, Error = "not reachable" });
        return tls;
    }

    private static TraefikRouter EnabledRouter(
        string name = "app@docker",
        string rule = "Host(`app.example.com`)",
        bool tls = false,
        string service = "app-service@docker",
        string[]? entryPoints = null,
        string[]? middlewares = null) => new()
    {
        Name = name,
        Rule = rule,
        Status = "enabled",
        EntryPoints = entryPoints ?? ["websecure"],
        Service = service,
        Middlewares = middlewares ?? [],
        HasTls = tls,
        Hostnames = ["app.example.com"]
    };

    private static TraefikService HealthyService(string name = "app-service@docker") => new()
    {
        Name = name,
        Status = "enabled",
        UsedBy = ["app@docker"],
        ServerUrls = ["http://backend:80"],
        ServerStatus = new Dictionary<string, string> { ["http://backend:80"] = "UP" }
    };

    // --- Availability ---

    [Fact]
    public async Task Returns_unavailable_when_traefik_api_not_reachable()
    {
        var client = Substitute.For<ITraefikClient>();
        client.GetAvailabilityAsync(default).ReturnsForAnyArgs(new TraefikAvailability
        {
            Status = TraefikAvailabilityStatus.Unavailable,
            Message = "Cannot reach Traefik API at http://localhost:8080."
        });

        var check = new TraefikCheck(client, NoOpTls());
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Returns_unavailable_when_api_requires_auth()
    {
        var client = Substitute.For<ITraefikClient>();
        client.GetAvailabilityAsync(default).ReturnsForAnyArgs(new TraefikAvailability
        {
            Status = TraefikAvailabilityStatus.Unauthorized,
            Message = "Traefik API requires authentication."
        });

        var check = new TraefikCheck(client, NoOpTls());
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Unavailable, result.Status);
        Assert.Contains("authentication", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    // --- Router checks ---

    [Fact]
    public async Task Produces_critical_finding_for_disabled_router()
    {
        var client = Substitute.For<ITraefikClient>();
        client.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        client.GetHttpRoutersAsync(default).ReturnsForAnyArgs(new List<TraefikRouter>
        {
            new()
            {
                Name = "broken-app@docker",
                Rule = "Host(`broken.example.com`)",
                Status = "disabled",
                EntryPoints = ["websecure"],
                Service = "broken-service@docker",
                Middlewares = [],
                HasTls = true,
                Hostnames = ["broken.example.com"]
            }
        });
        client.GetHttpServicesAsync(default).ReturnsForAnyArgs(new List<TraefikService>());
        client.GetHttpMiddlewaresAsync(default).ReturnsForAnyArgs(new List<TraefikMiddleware>());

        var check = new TraefikCheck(client, NoOpTls());
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Critical, result.Status);
        Assert.Contains(result.Findings, f => f.Id.StartsWith("TRAEFIK_ROUTER_ERRORED_") && f.Severity == Severity.Critical);
    }

    [Fact]
    public async Task Produces_critical_finding_for_router_pointing_to_missing_service()
    {
        var client = Substitute.For<ITraefikClient>();
        client.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        client.GetHttpRoutersAsync(default).ReturnsForAnyArgs(new List<TraefikRouter>
        {
            EnabledRouter(service: "ghost-service@docker")
        });
        client.GetHttpServicesAsync(default).ReturnsForAnyArgs(new List<TraefikService>
        {
            HealthyService("different-service@docker")
        });
        client.GetHttpMiddlewaresAsync(default).ReturnsForAnyArgs(new List<TraefikMiddleware>());

        var check = new TraefikCheck(client, NoOpTls());
        var result = await check.RunAsync();

        Assert.Contains(result.Findings, f => f.Id.StartsWith("TRAEFIK_ROUTER_MISSING_SERVICE_") && f.Severity == Severity.Critical);
    }

    [Fact]
    public async Task Produces_warning_for_http_router_without_https_redirect()
    {
        var client = Substitute.For<ITraefikClient>();
        client.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        client.GetHttpRoutersAsync(default).ReturnsForAnyArgs(new List<TraefikRouter>
        {
            EnabledRouter(entryPoints: ["web"], tls: false, middlewares: [])
        });
        client.GetHttpServicesAsync(default).ReturnsForAnyArgs(new List<TraefikService> { HealthyService() });
        client.GetHttpMiddlewaresAsync(default).ReturnsForAnyArgs(new List<TraefikMiddleware>());

        var check = new TraefikCheck(client, NoOpTls());
        var result = await check.RunAsync();

        Assert.Contains(result.Findings, f => f.Id == "TRAEFIK_HTTP_NO_REDIRECT" && f.Severity == Severity.Warning);
    }

    [Fact]
    public async Task Does_not_warn_about_http_router_that_has_redirect_middleware()
    {
        var redirectMiddleware = new TraefikMiddleware
        {
            Name = "redirect-to-https@file",
            Status = "enabled",
            UsedBy = ["app@docker"],
            Type = "redirectscheme"
        };

        var client = Substitute.For<ITraefikClient>();
        client.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        client.GetHttpRoutersAsync(default).ReturnsForAnyArgs(new List<TraefikRouter>
        {
            EnabledRouter(entryPoints: ["web"], tls: false, middlewares: ["redirect-to-https@file"])
        });
        client.GetHttpServicesAsync(default).ReturnsForAnyArgs(new List<TraefikService> { HealthyService() });
        client.GetHttpMiddlewaresAsync(default).ReturnsForAnyArgs(new List<TraefikMiddleware> { redirectMiddleware });

        var check = new TraefikCheck(client, NoOpTls());
        var result = await check.RunAsync();

        Assert.DoesNotContain(result.Findings, f => f.Id == "TRAEFIK_HTTP_NO_REDIRECT");
    }

    // --- Service checks ---

    [Fact]
    public async Task Produces_critical_finding_when_all_backends_down()
    {
        var client = Substitute.For<ITraefikClient>();
        client.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        client.GetHttpRoutersAsync(default).ReturnsForAnyArgs(new List<TraefikRouter> { EnabledRouter() });
        client.GetHttpServicesAsync(default).ReturnsForAnyArgs(new List<TraefikService>
        {
            new()
            {
                Name = "app-service@docker",
                Status = "enabled",
                UsedBy = ["app@docker"],
                ServerUrls = ["http://backend:80"],
                ServerStatus = new Dictionary<string, string> { ["http://backend:80"] = "DOWN" }
            }
        });
        client.GetHttpMiddlewaresAsync(default).ReturnsForAnyArgs(new List<TraefikMiddleware>());

        var check = new TraefikCheck(client, NoOpTls());
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Critical, result.Status);
        Assert.Contains(result.Findings, f => f.Id.StartsWith("TRAEFIK_SERVICE_DOWN_") && f.Severity == Severity.Critical);
    }

    [Fact]
    public async Task Produces_warning_when_some_backends_down()
    {
        var client = Substitute.For<ITraefikClient>();
        client.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        client.GetHttpRoutersAsync(default).ReturnsForAnyArgs(new List<TraefikRouter> { EnabledRouter() });
        client.GetHttpServicesAsync(default).ReturnsForAnyArgs(new List<TraefikService>
        {
            new()
            {
                Name = "app-service@docker",
                Status = "enabled",
                UsedBy = ["app@docker"],
                ServerUrls = ["http://backend1:80", "http://backend2:80"],
                ServerStatus = new Dictionary<string, string>
                {
                    ["http://backend1:80"] = "UP",
                    ["http://backend2:80"] = "DOWN"
                }
            }
        });
        client.GetHttpMiddlewaresAsync(default).ReturnsForAnyArgs(new List<TraefikMiddleware>());

        var check = new TraefikCheck(client, NoOpTls());
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Contains(result.Findings, f => f.Id.StartsWith("TRAEFIK_SERVICE_DEGRADED_") && f.Severity == Severity.Warning);
    }

    // --- TLS checks ---

    [Fact]
    public async Task Produces_warning_for_cert_expiring_within_30_days()
    {
        var tls = Substitute.For<ITlsInspector>();
        tls.CheckAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TlsCertInfo
            {
                Hostname = "app.example.com",
                Connected = true,
                ExpiresAt = DateTime.UtcNow.AddDays(20)
            });

        var client = Substitute.For<ITraefikClient>();
        client.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        client.GetHttpRoutersAsync(default).ReturnsForAnyArgs(new List<TraefikRouter>
        {
            EnabledRouter(tls: true)
        });
        client.GetHttpServicesAsync(default).ReturnsForAnyArgs(new List<TraefikService> { HealthyService() });
        client.GetHttpMiddlewaresAsync(default).ReturnsForAnyArgs(new List<TraefikMiddleware>());

        var check = new TraefikCheck(client, tls);
        var result = await check.RunAsync();

        Assert.Contains(result.Findings, f => f.Id.StartsWith("TRAEFIK_TLS_EXPIRING_") && f.Severity == Severity.Warning);
    }

    [Fact]
    public async Task Produces_critical_for_cert_expiring_within_7_days()
    {
        var tls = Substitute.For<ITlsInspector>();
        tls.CheckAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TlsCertInfo
            {
                Hostname = "app.example.com",
                Connected = true,
                ExpiresAt = DateTime.UtcNow.AddDays(3)
            });

        var client = Substitute.For<ITraefikClient>();
        client.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        client.GetHttpRoutersAsync(default).ReturnsForAnyArgs(new List<TraefikRouter> { EnabledRouter(tls: true) });
        client.GetHttpServicesAsync(default).ReturnsForAnyArgs(new List<TraefikService> { HealthyService() });
        client.GetHttpMiddlewaresAsync(default).ReturnsForAnyArgs(new List<TraefikMiddleware>());

        var check = new TraefikCheck(client, tls);
        var result = await check.RunAsync();

        Assert.Contains(result.Findings, f => f.Id.StartsWith("TRAEFIK_TLS_EXPIRING_") && f.Severity == Severity.Critical);
    }

    [Fact]
    public async Task Produces_critical_for_expired_cert()
    {
        var tls = Substitute.For<ITlsInspector>();
        tls.CheckAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TlsCertInfo
            {
                Hostname = "app.example.com",
                Connected = true,
                ExpiresAt = DateTime.UtcNow.AddDays(-5)
            });

        var client = Substitute.For<ITraefikClient>();
        client.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        client.GetHttpRoutersAsync(default).ReturnsForAnyArgs(new List<TraefikRouter> { EnabledRouter(tls: true) });
        client.GetHttpServicesAsync(default).ReturnsForAnyArgs(new List<TraefikService> { HealthyService() });
        client.GetHttpMiddlewaresAsync(default).ReturnsForAnyArgs(new List<TraefikMiddleware>());

        var check = new TraefikCheck(client, tls);
        var result = await check.RunAsync();

        Assert.Contains(result.Findings, f => f.Id.StartsWith("TRAEFIK_TLS_EXPIRED_") && f.Severity == Severity.Critical);
    }

    [Fact]
    public async Task Skips_tls_check_for_unreachable_host()
    {
        var tls = Substitute.For<ITlsInspector>();
        tls.CheckAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TlsCertInfo { Hostname = "internal.home.arpa", Connected = false, Error = "connection refused" });

        var client = Substitute.For<ITraefikClient>();
        client.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        client.GetHttpRoutersAsync(default).ReturnsForAnyArgs(new List<TraefikRouter> { EnabledRouter(tls: true) });
        client.GetHttpServicesAsync(default).ReturnsForAnyArgs(new List<TraefikService> { HealthyService() });
        client.GetHttpMiddlewaresAsync(default).ReturnsForAnyArgs(new List<TraefikMiddleware>());

        var check = new TraefikCheck(client, tls);
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.DoesNotContain(result.Findings, f => f.Id.StartsWith("TRAEFIK_TLS_"));
    }

    // --- Happy path ---

    [Fact]
    public async Task Returns_passed_when_all_healthy()
    {
        var client = Substitute.For<ITraefikClient>();
        client.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        client.GetHttpRoutersAsync(default).ReturnsForAnyArgs(new List<TraefikRouter>
        {
            EnabledRouter(tls: true, entryPoints: ["websecure"])
        });
        client.GetHttpServicesAsync(default).ReturnsForAnyArgs(new List<TraefikService> { HealthyService() });
        client.GetHttpMiddlewaresAsync(default).ReturnsForAnyArgs(new List<TraefikMiddleware>());

        var check = new TraefikCheck(client, NoOpTls());
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Passed, result.Status);
    }
}
