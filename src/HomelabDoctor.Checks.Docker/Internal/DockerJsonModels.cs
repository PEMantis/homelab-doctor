using System.Text.Json.Serialization;

namespace HomelabDoctor.Checks.Docker.Internal;

// docker ps -a --format '{{json .}}' — one object per line
internal sealed class PsRow
{
    [JsonPropertyName("ID")] public string Id { get; set; } = "";
    [JsonPropertyName("Names")] public string Names { get; set; } = "";
    [JsonPropertyName("Image")] public string Image { get; set; } = "";
    [JsonPropertyName("State")] public string State { get; set; } = "";
    [JsonPropertyName("Status")] public string Status { get; set; } = "";
    [JsonPropertyName("Ports")] public string Ports { get; set; } = "";
}

// docker inspect <ids> — returns an array
internal sealed class InspectData
{
    [JsonPropertyName("Id")] public string Id { get; set; } = "";
    [JsonPropertyName("Name")] public string Name { get; set; } = "";
    [JsonPropertyName("RestartCount")] public int RestartCount { get; set; }
    [JsonPropertyName("State")] public InspectState State { get; set; } = new();
    [JsonPropertyName("Config")] public InspectConfig Config { get; set; } = new();
    [JsonPropertyName("NetworkSettings")] public InspectNetworkSettings NetworkSettings { get; set; } = new();
}

internal sealed class InspectState
{
    [JsonPropertyName("Status")] public string Status { get; set; } = "";
    [JsonPropertyName("Running")] public bool Running { get; set; }
    [JsonPropertyName("Restarting")] public bool Restarting { get; set; }
    [JsonPropertyName("Health")] public InspectHealth? Health { get; set; }
}

internal sealed class InspectHealth
{
    [JsonPropertyName("Status")] public string Status { get; set; } = "";
    [JsonPropertyName("FailingStreak")] public int FailingStreak { get; set; }
}

internal sealed class InspectConfig
{
    [JsonPropertyName("Healthcheck")] public InspectHealthcheck? Healthcheck { get; set; }
}

internal sealed class InspectHealthcheck
{
    [JsonPropertyName("Test")] public List<string>? Test { get; set; }
}

internal sealed class InspectNetworkSettings
{
    [JsonPropertyName("Ports")]
    public Dictionary<string, List<PortBinding>?>? Ports { get; set; }
}

internal sealed class PortBinding
{
    [JsonPropertyName("HostIp")] public string HostIp { get; set; } = "";
    [JsonPropertyName("HostPort")] public string HostPort { get; set; } = "";
}
