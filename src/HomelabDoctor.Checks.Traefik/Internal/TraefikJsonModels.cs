using System.Text.Json.Serialization;

namespace HomelabDoctor.Checks.Traefik.Internal;

internal sealed class RouterJson
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("rule")] public string Rule { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("entryPoints")] public List<string>? EntryPoints { get; set; }
    [JsonPropertyName("service")] public string Service { get; set; } = "";
    [JsonPropertyName("middlewares")] public List<string>? Middlewares { get; set; }
    [JsonPropertyName("tls")] public RouterTlsJson? Tls { get; set; }
}

internal sealed class RouterTlsJson
{
    [JsonPropertyName("certResolver")] public string? CertResolver { get; set; }
}

internal sealed class ServiceJson
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("usedBy")] public List<string>? UsedBy { get; set; }
    [JsonPropertyName("loadBalancer")] public LoadBalancerJson? LoadBalancer { get; set; }
    [JsonPropertyName("serverStatus")] public Dictionary<string, string>? ServerStatus { get; set; }
}

internal sealed class LoadBalancerJson
{
    [JsonPropertyName("servers")] public List<ServerJson>? Servers { get; set; }
}

internal sealed class ServerJson
{
    [JsonPropertyName("url")] public string Url { get; set; } = "";
}

internal sealed class MiddlewareJson
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("usedBy")] public List<string>? UsedBy { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
}
