using HomelabDoctor.Checks.Traefik.Models;
using HomelabDoctor.Core.Interfaces;
using HomelabDoctor.Core.Models;

namespace HomelabDoctor.Checks.Traefik;

public sealed class TraefikCheck : ICheck
{
    public string Name => "Traefik";

    private const int TlsWarnDays = 30;
    private const int TlsCriticalDays = 7;

    private readonly ITraefikClient _client;
    private readonly ITlsInspector _tls;

    public TraefikCheck(ITraefikClient client, ITlsInspector tls)
    {
        _client = client;
        _tls = tls;
    }

    public async Task<CheckResult> RunAsync(CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;

        var availability = await _client.GetAvailabilityAsync(ct);
        if (availability.Status != TraefikAvailabilityStatus.Available)
        {
            return CheckResult.Unavailable("Traefik",
                availability.Message ?? "Traefik API is not reachable.", startedAt);
        }

        var findings = new List<Finding>();

        var routers = await _client.GetHttpRoutersAsync(ct);
        var services = await _client.GetHttpServicesAsync(ct);
        var middlewares = await _client.GetHttpMiddlewaresAsync(ct);

        findings.Add(new Finding
        {
            Id = "TRAEFIK_AVAILABLE",
            Title = $"Traefik API reachable — {routers.Count} router(s), {services.Count} service(s), {middlewares.Count} middleware(s)",
            Severity = Severity.Info,
            Category = "Availability",
            Evidence = availability.ApiUrl ?? "http://localhost:8080",
            Explanation = "Traefik is running and the API responded successfully."
        });

        CheckRouters(routers, services, middlewares, findings);
        CheckServices(services, findings);
        await CheckTlsCerts(routers, findings, ct);

        var worstSeverity = findings
            .Where(f => f.Id != "TRAEFIK_AVAILABLE")
            .Select(f => f.Severity)
            .DefaultIfEmpty(Severity.Info)
            .Max();

        var status = worstSeverity switch
        {
            Severity.Critical => CheckStatus.Critical,
            Severity.Warning => CheckStatus.Warning,
            _ => CheckStatus.Passed
        };

        var criticals = findings.Count(f => f.Severity == Severity.Critical);
        var warnings = findings.Count(f => f.Severity == Severity.Warning);
        var summary = (criticals, warnings) switch
        {
            (0, 0) => $"Traefik looks healthy. {routers.Count} router(s), {services.Count} service(s).",
            (var c, 0) => $"{c} critical issue(s) across {routers.Count} router(s) and {services.Count} service(s).",
            (0, var w) => $"{w} warning(s) across {routers.Count} router(s) and {services.Count} service(s).",
            (var c, var w) => $"{c} critical issue(s) and {w} warning(s)."
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

    private static void CheckRouters(
        IReadOnlyList<TraefikRouter> routers,
        IReadOnlyList<TraefikService> services,
        IReadOnlyList<TraefikMiddleware> middlewares,
        List<Finding> findings)
    {
        var serviceNames = services.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var redirectMiddlewareNames = middlewares
            .Where(m => m.Type.Contains("redirectscheme", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var router in routers.Where(r => r.Status == "disabled"))
        {
            findings.Add(new Finding
            {
                Id = $"TRAEFIK_ROUTER_ERRORED_{Sanitize(router.Name)}",
                Title = $"Router {ShortName(router.Name)} is disabled",
                Severity = Severity.Critical,
                Category = "Routers",
                Evidence = $"Status: disabled. Rule: {router.Rule}",
                Explanation = "Traefik rejected this router's configuration. A disabled router means no traffic is being routed for the matched hostnames.",
                SuggestedFix = "Check the Traefik logs for the specific configuration error and fix the router definition.",
                SuggestedCommands = [$"docker logs traefik --tail=100 | grep -i '{ShortName(router.Name)}'"]
            });
        }

        // Routers pointing to services that don't exist in the service list
        foreach (var router in routers.Where(r => r.Status == "enabled" && !string.IsNullOrEmpty(r.Service)))
        {
            // Traefik auto-generates a service name like "my-service@docker" — check both short and full forms
            var matchedService = serviceNames.Contains(router.Service)
                || serviceNames.Any(sn => sn.StartsWith(router.Service + "@", StringComparison.OrdinalIgnoreCase));
            if (!matchedService)
            {
                findings.Add(new Finding
                {
                    Id = $"TRAEFIK_ROUTER_MISSING_SERVICE_{Sanitize(router.Name)}",
                    Title = $"Router {ShortName(router.Name)} points to missing service '{router.Service}'",
                    Severity = Severity.Critical,
                    Category = "Routers",
                    Evidence = $"Router rule: {router.Rule}. Referenced service: {router.Service}",
                    Explanation = "This router references a service that no longer exists. Requests matching this router will return a 404 or 503.",
                    SuggestedFix = "Update the router to point to an existing service, or remove it.",
                    SuggestedCommands = [$"docker logs traefik --tail=100 | grep -i '{ShortName(router.Name)}'"]
                });
            }
        }

        // HTTP routers without a redirect-to-HTTPS middleware
        var insecureRouters = routers
            .Where(r => r.Status == "enabled"
                && !r.HasTls
                && r.EntryPoints.Any(e => IsPlainHttpEntrypoint(e))
                && !r.Middlewares.Any(m => redirectMiddlewareNames.Contains(m)
                    || redirectMiddlewareNames.Any(rm => rm.StartsWith(m + "@", StringComparison.OrdinalIgnoreCase))))
            .ToList();

        if (insecureRouters.Count > 0)
        {
            findings.Add(new Finding
            {
                Id = "TRAEFIK_HTTP_NO_REDIRECT",
                Title = $"{insecureRouters.Count} router(s) serve HTTP without a redirect to HTTPS",
                Severity = Severity.Warning,
                Category = "Security",
                Evidence = string.Join(", ", insecureRouters.Select(r => ShortName(r.Name))),
                Explanation = "These routers are reachable over plain HTTP and do not apply a redirectScheme middleware. Traffic to them is unencrypted.",
                SuggestedFix = "Add a redirectScheme middleware that permanently redirects HTTP to HTTPS.",
                DocumentationUrl = "https://doc.traefik.io/traefik/middlewares/http/redirectscheme/"
            });
        }
    }

    private static void CheckServices(IReadOnlyList<TraefikService> services, List<Finding> findings)
    {
        foreach (var service in services)
        {
            // Skip services with no health status data (no healthcheck configured)
            if (service.TotalServerCount == 0) continue;

            if (service.HealthyServerCount == 0)
            {
                findings.Add(new Finding
                {
                    Id = $"TRAEFIK_SERVICE_DOWN_{Sanitize(service.Name)}",
                    Title = $"Service {ShortName(service.Name)} has no healthy backends",
                    Severity = Severity.Critical,
                    Category = "Services",
                    Evidence = $"All {service.TotalServerCount} backend(s) are DOWN: {string.Join(", ", service.ServerStatus.Keys.Take(3))}",
                    Explanation = "Every backend server for this service is reporting DOWN. Any router using this service will return errors to clients.",
                    SuggestedFix = "Check that the containers this service routes to are running and reachable from the Traefik network.",
                    SuggestedCommands = service.ServerUrls.Take(3).Select(u => $"curl -v {u}").ToList()
                });
            }
            else if (service.HealthyServerCount < service.TotalServerCount)
            {
                var downCount = service.TotalServerCount - service.HealthyServerCount;
                findings.Add(new Finding
                {
                    Id = $"TRAEFIK_SERVICE_DEGRADED_{Sanitize(service.Name)}",
                    Title = $"Service {ShortName(service.Name)} is degraded ({service.HealthyServerCount}/{service.TotalServerCount} backends up)",
                    Severity = Severity.Warning,
                    Category = "Services",
                    Evidence = $"{downCount} of {service.TotalServerCount} backend(s) are DOWN",
                    Explanation = "Some backends are unreachable. Traefik is routing to the healthy ones, but capacity is reduced. Investigate the down backends.",
                    SuggestedCommands = service.ServerUrls.Take(3).Select(u => $"curl -v {u}").ToList()
                });
            }
        }
    }

    private async Task CheckTlsCerts(
        IReadOnlyList<TraefikRouter> routers,
        List<Finding> findings,
        CancellationToken ct)
    {
        var tlsHostnames = routers
            .Where(r => r.HasTls && r.Status == "enabled")
            .SelectMany(r => r.Hostnames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var hostname in tlsHostnames)
        {
            var cert = await _tls.CheckAsync(hostname, 443, ct);

            if (!cert.Connected) continue; // Private/internal hosts we can't reach — skip silently

            if (cert.DaysUntilExpiry is null) continue;

            if (cert.DaysUntilExpiry <= 0)
            {
                findings.Add(new Finding
                {
                    Id = $"TRAEFIK_TLS_EXPIRED_{Sanitize(hostname)}",
                    Title = $"TLS certificate for {hostname} has expired",
                    Severity = Severity.Critical,
                    Category = "TLS",
                    Evidence = $"Expired: {cert.ExpiresAt:yyyy-MM-dd}",
                    Explanation = "Browsers will show a hard security warning for this hostname. The certificate must be renewed immediately.",
                    SuggestedFix = "If using Let's Encrypt via Traefik, check that the ACME challenge is completing successfully.",
                    SuggestedCommands =
                    [
                        "docker logs traefik --tail=200 | grep -i 'acme\\|certificate\\|renewal'",
                        $"openssl s_client -connect {hostname}:443 -servername {hostname} </dev/null 2>/dev/null | openssl x509 -noout -dates"
                    ]
                });
            }
            else if (cert.DaysUntilExpiry <= TlsCriticalDays)
            {
                findings.Add(new Finding
                {
                    Id = $"TRAEFIK_TLS_EXPIRING_{Sanitize(hostname)}",
                    Title = $"TLS certificate for {hostname} expires in {cert.DaysUntilExpiry} day(s)",
                    Severity = Severity.Critical,
                    Category = "TLS",
                    Evidence = $"Expiry: {cert.ExpiresAt:yyyy-MM-dd}. Days remaining: {cert.DaysUntilExpiry}",
                    Explanation = $"The certificate for {hostname} expires very soon. Automatic renewal may have failed — investigate immediately.",
                    SuggestedFix = "Check ACME/Let's Encrypt renewal logs.",
                    SuggestedCommands = ["docker logs traefik --tail=200 | grep -i 'acme\\|certificate\\|renewal'"]
                });
            }
            else if (cert.DaysUntilExpiry <= TlsWarnDays)
            {
                findings.Add(new Finding
                {
                    Id = $"TRAEFIK_TLS_EXPIRING_{Sanitize(hostname)}",
                    Title = $"TLS certificate for {hostname} expires in {cert.DaysUntilExpiry} day(s)",
                    Severity = Severity.Warning,
                    Category = "TLS",
                    Evidence = $"Expiry: {cert.ExpiresAt:yyyy-MM-dd}. Days remaining: {cert.DaysUntilExpiry}",
                    Explanation = $"The certificate for {hostname} will expire in under 30 days. If using Let's Encrypt, automatic renewal should trigger around 30 days before expiry.",
                    SuggestedFix = "Verify the ACME challenge is working and the cert resolver is configured correctly."
                });
            }
        }
    }

    private static string Sanitize(string name) =>
        name.Replace("@", "_").Replace(".", "_").Replace("-", "_").ToUpperInvariant();

    private static string ShortName(string name) =>
        name.Contains('@') ? name[..name.IndexOf('@')] : name;

    private static bool IsPlainHttpEntrypoint(string ep) =>
        ep.Equals("web", StringComparison.OrdinalIgnoreCase)
        || ep.Equals("http", StringComparison.OrdinalIgnoreCase)
        || (ep.Contains("80", StringComparison.OrdinalIgnoreCase) && !ep.Contains("secure", StringComparison.OrdinalIgnoreCase));
}
