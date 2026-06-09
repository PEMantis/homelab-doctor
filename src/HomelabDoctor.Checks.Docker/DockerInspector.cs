using System.Text.Json;
using HomelabDoctor.Checks.Docker.Internal;
using HomelabDoctor.Checks.Docker.Models;
using HomelabDoctor.Core.Interfaces;

namespace HomelabDoctor.Checks.Docker;

public sealed class DockerInspector : IDockerInspector
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly ICommandRunner _runner;

    public DockerInspector(ICommandRunner runner) => _runner = runner;

    public async Task<DockerAvailability> GetAvailabilityAsync(CancellationToken ct = default)
    {
        CommandOutput output;
        try
        {
            output = await _runner.RunAsync("docker", "info --format json", ct);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new DockerAvailability { Status = DockerAvailabilityStatus.NotInstalled, Message = "docker executable not found" };
        }

        if (output.ExitCode != 0)
        {
            var combined = (output.Stderr + " " + output.Stdout).ToLower();
            if (combined.Contains("permission denied") || combined.Contains("access denied"))
                return new DockerAvailability
                {
                    Status = DockerAvailabilityStatus.PermissionDenied,
                    Message = "Permission denied when connecting to Docker daemon. Your user may need to be added to the docker group."
                };
            if (combined.Contains("cannot connect") || combined.Contains("is the docker daemon running") || combined.Contains("no such file or directory") && combined.Contains("docker.sock"))
                return new DockerAvailability
                {
                    Status = DockerAvailabilityStatus.DaemonNotRunning,
                    Message = "Cannot connect to the Docker daemon. Is the Docker service running?"
                };
            return new DockerAvailability { Status = DockerAvailabilityStatus.Unknown, Message = output.Stderr.Trim() };
        }

        var (serverVersion, containerCount, runningCount, imageCount) = ParseDockerInfo(output.Stdout);
        return new DockerAvailability
        {
            Status = DockerAvailabilityStatus.Available,
            ServerVersion = serverVersion,
            ContainerCount = containerCount,
            RunningCount = runningCount,
            ImageCount = imageCount
        };
    }

    public async Task<IReadOnlyList<ContainerSummary>> GetContainersAsync(CancellationToken ct = default)
    {
        var psOutput = await _runner.RunAsync("docker", "ps -a --format '{{json .}}'", ct);
        if (psOutput.ExitCode != 0 || string.IsNullOrWhiteSpace(psOutput.Stdout))
            return [];

        var rows = ParsePsOutput(psOutput.Stdout);
        if (rows.Count == 0)
            return [];

        var ids = string.Join(" ", rows.Select(r => r.Id));
        var inspectOutput = await _runner.RunAsync("docker", $"inspect {ids}", ct);

        var inspectMap = ParseInspectOutput(inspectOutput.Stdout);

        return rows.Select(row =>
        {
            var inspect = inspectMap.GetValueOrDefault(row.Id) ?? inspectMap.Values.FirstOrDefault(i => i.Id.StartsWith(row.Id));
            return BuildSummary(row, inspect);
        }).ToList();
    }

    private static (string? serverVersion, int? containerCount, int? runningCount, int? imageCount) ParseDockerInfo(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var version = root.TryGetProperty("ServerVersion", out var sv) ? sv.GetString() : null;
            var containers = root.TryGetProperty("Containers", out var c) && c.TryGetInt32(out var ci) ? (int?)ci : null;
            var running = root.TryGetProperty("ContainersRunning", out var r) && r.TryGetInt32(out var ri) ? (int?)ri : null;
            var images = root.TryGetProperty("Images", out var img) && img.TryGetInt32(out var ii) ? (int?)ii : null;
            return (version, containers, running, images);
        }
        catch
        {
            return (null, null, null, null);
        }
    }

    private static List<PsRow> ParsePsOutput(string stdout)
    {
        var rows = new List<PsRow>();
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith('{')) continue;
            try
            {
                var row = JsonSerializer.Deserialize<PsRow>(line, JsonOpts);
                if (row is not null && !string.IsNullOrEmpty(row.Id))
                    rows.Add(row);
            }
            catch { /* skip malformed lines */ }
        }
        return rows;
    }

    private static Dictionary<string, InspectData> ParseInspectOutput(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var items = JsonSerializer.Deserialize<List<InspectData>>(json, JsonOpts) ?? [];
            return items.ToDictionary(i => i.Id, i => i);
        }
        catch { return []; }
    }

    private static ContainerSummary BuildSummary(PsRow row, InspectData? inspect)
    {
        var name = row.Names.TrimStart('/');
        var restartCount = inspect?.RestartCount ?? 0;
        var healthStatus = inspect?.State?.Health?.Status;

        bool hasHealthcheck = false;
        if (inspect?.Config?.Healthcheck is { } hc)
        {
            // NONE means explicitly disabled
            hasHealthcheck = hc.Test is null || hc.Test.Count == 0 || !string.Equals(hc.Test[0], "NONE", StringComparison.OrdinalIgnoreCase);
        }

        var publicPorts = new List<string>();
        if (inspect?.NetworkSettings?.Ports != null)
        {
            foreach (var (portProto, bindings) in inspect.NetworkSettings.Ports)
            {
                if (bindings is null) continue;
                foreach (var binding in bindings)
                {
                    if (binding.HostIp == "0.0.0.0" || binding.HostIp == "::")
                        publicPorts.Add($"{binding.HostIp}:{binding.HostPort}->{portProto}");
                }
            }
        }
        else if (!string.IsNullOrEmpty(row.Ports))
        {
            foreach (var part in row.Ports.Split(',', StringSplitOptions.TrimEntries))
            {
                if (part.Contains("0.0.0.0") || part.StartsWith(":::"))
                    publicPorts.Add(part);
            }
        }

        return new ContainerSummary
        {
            Id = row.Id,
            Name = name,
            Image = row.Image,
            State = row.State,
            Status = row.Status,
            RestartCount = restartCount,
            HasHealthcheck = hasHealthcheck,
            HealthStatus = healthStatus,
            PublicPorts = publicPorts
        };
    }
}
