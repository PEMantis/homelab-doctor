using System.Net;
using System.Text.Json;
using HomelabDoctor.Checks.Traefik.Internal;
using HomelabDoctor.Checks.Traefik.Models;
using System.Text.RegularExpressions;

namespace HomelabDoctor.Checks.Traefik;

public sealed class TraefikClient : ITraefikClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    // Matches Host(`hostname`) — backtick-delimited, handles multiple hosts in one matcher
    private static readonly Regex HostRule = new(@"Host\(`([^`]+)`\)", RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public TraefikClient(string? baseUrl = null)
    {
        _baseUrl = (baseUrl ?? ResolveBaseUrl()).TrimEnd('/');
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    private static string ResolveBaseUrl()
    {
        var env = Environment.GetEnvironmentVariable("TRAEFIK_API_URL");
        return !string.IsNullOrWhiteSpace(env) ? env : "http://localhost:8080";
    }

    public async Task<TraefikAvailability> GetAvailabilityAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/api/overview", ct);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => new TraefikAvailability { Status = TraefikAvailabilityStatus.Available, ApiUrl = _baseUrl },
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new TraefikAvailability
                {
                    Status = TraefikAvailabilityStatus.Unauthorized,
                    ApiUrl = _baseUrl,
                    Message = $"Traefik API at {_baseUrl} requires authentication. Enable insecureAPI or configure access credentials."
                },
                _ => new TraefikAvailability
                {
                    Status = TraefikAvailabilityStatus.Unavailable,
                    ApiUrl = _baseUrl,
                    Message = $"Traefik API returned {(int)response.StatusCode} {response.ReasonPhrase}."
                }
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new TraefikAvailability
            {
                Status = TraefikAvailabilityStatus.Unavailable,
                ApiUrl = _baseUrl,
                Message = $"Cannot reach Traefik API at {_baseUrl}. Is Traefik running with --api=true?"
            };
        }
    }

    public async Task<IReadOnlyList<TraefikRouter>> GetHttpRoutersAsync(CancellationToken ct = default)
    {
        var json = await FetchAsync($"{_baseUrl}/api/http/routers", ct);
        if (json is null) return [];
        var rows = JsonSerializer.Deserialize<List<RouterJson>>(json, JsonOpts) ?? [];
        return rows.Select(MapRouter).ToList();
    }

    public async Task<IReadOnlyList<TraefikService>> GetHttpServicesAsync(CancellationToken ct = default)
    {
        var json = await FetchAsync($"{_baseUrl}/api/http/services", ct);
        if (json is null) return [];
        var rows = JsonSerializer.Deserialize<List<ServiceJson>>(json, JsonOpts) ?? [];
        return rows.Select(MapService).ToList();
    }

    public async Task<IReadOnlyList<TraefikMiddleware>> GetHttpMiddlewaresAsync(CancellationToken ct = default)
    {
        var json = await FetchAsync($"{_baseUrl}/api/http/middlewares", ct);
        if (json is null) return [];
        var rows = JsonSerializer.Deserialize<List<MiddlewareJson>>(json, JsonOpts) ?? [];
        return rows.Select(MapMiddleware).ToList();
    }

    private async Task<string?> FetchAsync(string url, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync(url, ct);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(ct) : null;
        }
        catch { return null; }
    }

    private TraefikRouter MapRouter(RouterJson r) => new()
    {
        Name = r.Name,
        Rule = r.Rule,
        Status = r.Status,
        EntryPoints = r.EntryPoints ?? [],
        Service = r.Service,
        Middlewares = r.Middlewares ?? [],
        HasTls = r.Tls is not null,
        TlsCertResolver = r.Tls?.CertResolver,
        Hostnames = ExtractHostnames(r.Rule)
    };

    private static TraefikService MapService(ServiceJson s) => new()
    {
        Name = s.Name,
        Status = s.Status,
        UsedBy = s.UsedBy ?? [],
        ServerUrls = s.LoadBalancer?.Servers?.Select(sv => sv.Url).ToList() ?? [],
        ServerStatus = s.ServerStatus ?? new Dictionary<string, string>()
    };

    private static TraefikMiddleware MapMiddleware(MiddlewareJson m) => new()
    {
        Name = m.Name,
        Status = m.Status,
        UsedBy = m.UsedBy ?? [],
        Type = m.Type ?? "unknown"
    };

    private IReadOnlyList<string> ExtractHostnames(string rule)
    {
        return HostRule.Matches(rule)
            .SelectMany(m => m.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries))
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToList();
    }

    public void Dispose() => _http.Dispose();
}
