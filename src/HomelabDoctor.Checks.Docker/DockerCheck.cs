using HomelabDoctor.Checks.Docker.Models;
using HomelabDoctor.Core.Interfaces;
using HomelabDoctor.Core.Models;

namespace HomelabDoctor.Checks.Docker;

public sealed class DockerCheck : ICheck
{
    public string Name => "Docker";

    private readonly IDockerInspector _inspector;

    public DockerCheck(IDockerInspector inspector) => _inspector = inspector;

    public async Task<CheckResult> RunAsync(CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
        var findings = new List<Finding>();

        var availability = await _inspector.GetAvailabilityAsync(ct);

        switch (availability.Status)
        {
            case DockerAvailabilityStatus.NotInstalled:
                return CheckResult.Unavailable("Docker", "Docker is not installed or not on PATH.", startedAt);

            case DockerAvailabilityStatus.PermissionDenied:
                return CheckResult.Unavailable("Docker",
                    availability.Message ?? "Permission denied when connecting to the Docker daemon.", startedAt);

            case DockerAvailabilityStatus.DaemonNotRunning:
                return CheckResult.Unavailable("Docker",
                    availability.Message ?? "Docker daemon is not running.", startedAt);

            case DockerAvailabilityStatus.Unknown:
                return CheckResult.Unavailable("Docker",
                    availability.Message ?? "Could not connect to Docker.", startedAt);
        }

        findings.Add(new Finding
        {
            Id = "DOCKER_AVAILABLE",
            Title = "Docker daemon is reachable",
            Severity = Severity.Info,
            Category = "Availability",
            Evidence = availability.ServerVersion is not null ? $"Server version: {availability.ServerVersion}" : "docker info succeeded",
            Explanation = "The Docker daemon responded successfully."
        });

        var containers = await _inspector.GetContainersAsync(ct);

        if (containers.Count == 0)
        {
            findings.Add(new Finding
            {
                Id = "DOCKER_NO_CONTAINERS",
                Title = "No containers found",
                Severity = Severity.Info,
                Category = "Containers",
                Evidence = "docker ps -a returned no containers",
                Explanation = "There are no containers, running or stopped."
            });
        }
        else
        {
            var running = containers.Count(c => c.State == "running");
            var stopped = containers.Count(c => c.State == "exited");
            var restarting = containers.Count(c => c.State == "restarting");
            var unhealthy = containers.Count(c => c.HealthStatus == "unhealthy");

            findings.Add(new Finding
            {
                Id = "DOCKER_CONTAINER_COUNTS",
                Title = $"{containers.Count} containers found",
                Severity = Severity.Info,
                Category = "Containers",
                Evidence = $"Running: {running}, Stopped: {stopped}, Restarting: {restarting}",
                Explanation = "Summary of all containers across all states."
            });

            if (availability.ImageCount is { } imgCount)
                findings.Add(new Finding
                {
                    Id = "DOCKER_IMAGE_COUNT",
                    Title = $"{imgCount} images on this host",
                    Severity = Severity.Info,
                    Category = "Images",
                    Evidence = $"docker info reported {imgCount} image(s)",
                    Explanation = "Total number of Docker images stored locally."
                });

            foreach (var c in containers.Where(c => c.State == "restarting"))
                findings.Add(new Finding
                {
                    Id = $"DOCKER_RESTARTING_{c.Name.ToUpperInvariant()}",
                    Title = $"Container {c.Name} is restarting",
                    Severity = Severity.Critical,
                    Category = "Container Health",
                    Evidence = $"State: restarting, Restart count: {c.RestartCount}",
                    Explanation = "Docker can restart a failed process, but repeated restarts usually mean the application is crashing at startup. Check the logs to find the root cause.",
                    SuggestedFix = "Check logs for the startup error, then fix the underlying application issue or configuration.",
                    SuggestedCommands = [$"docker logs --tail=100 {c.Name}", $"docker inspect {c.Name}"]
                });

            foreach (var c in containers.Where(c => c.HealthStatus == "unhealthy"))
                findings.Add(new Finding
                {
                    Id = $"DOCKER_UNHEALTHY_{c.Name.ToUpperInvariant()}",
                    Title = $"Container {c.Name} is unhealthy",
                    Severity = Severity.Critical,
                    Category = "Container Health",
                    Evidence = $"Healthcheck status: unhealthy",
                    Explanation = "The container's healthcheck is repeatedly failing. The service may be up but not responding correctly to its own health probe.",
                    SuggestedFix = "Review the healthcheck command and verify the service is responding correctly.",
                    SuggestedCommands = [$"docker inspect --format='{{{{json .State.Health}}}}' {c.Name}", $"docker logs --tail=100 {c.Name}"]
                });

            var noHealthcheck = containers.Where(c => c.State == "running" && !c.HasHealthcheck).ToList();
            if (noHealthcheck.Count > 0)
                findings.Add(new Finding
                {
                    Id = "DOCKER_NO_HEALTHCHECK",
                    Title = $"{noHealthcheck.Count} running container(s) have no healthcheck",
                    Severity = Severity.Warning,
                    Category = "Observability",
                    Evidence = string.Join(", ", noHealthcheck.Select(c => c.Name)),
                    Explanation = "Containers without healthchecks cannot report their own health to Docker. Docker will show them as 'running' even if the service inside has crashed silently.",
                    SuggestedFix = "Add a HEALTHCHECK instruction to the Dockerfile, or define healthcheck in your compose file.",
                    SuggestedCommands = noHealthcheck.Take(3).Select(c => $"docker inspect --format='{{{{.Config.Healthcheck}}}}' {c.Name}").ToList()
                });

            var exposedContainers = containers.Where(c => c.PublicPorts.Count > 0).ToList();
            if (exposedContainers.Count > 0)
                findings.Add(new Finding
                {
                    Id = "DOCKER_EXPOSED_PORTS",
                    Title = $"{exposedContainers.Count} container(s) expose ports on 0.0.0.0",
                    Severity = Severity.Warning,
                    Category = "Network Exposure",
                    Evidence = string.Join("; ", exposedContainers.Select(c => $"{c.Name}: {string.Join(", ", c.PublicPorts)}")),
                    Explanation = "Binding to 0.0.0.0 exposes the port on all network interfaces, including public ones. This may be intentional for services like a reverse proxy, but unintended for internal services.",
                    SuggestedFix = "If the service should not be publicly reachable, bind to 127.0.0.1 instead, or remove the port mapping and use an internal Docker network.",
                    DocumentationUrl = "https://docs.docker.com/network/"
                });

            var highRestarts = containers.Where(c => c.State != "restarting" && c.RestartCount >= 5).ToList();
            foreach (var c in highRestarts)
                findings.Add(new Finding
                {
                    Id = $"DOCKER_HIGH_RESTARTS_{c.Name.ToUpperInvariant()}",
                    Title = $"Container {c.Name} has restarted {c.RestartCount} times",
                    Severity = Severity.Warning,
                    Category = "Container Health",
                    Evidence = $"Restart count: {c.RestartCount}, Current state: {c.State}",
                    Explanation = "A high restart count suggests this container has experienced repeated failures in its history. It is currently stable, but worth investigating.",
                    SuggestedCommands = [$"docker logs --tail=100 {c.Name}"]
                });
        }

        var worstSeverity = findings.Where(f => f.Id != "DOCKER_AVAILABLE" && f.Id != "DOCKER_CONTAINER_COUNTS" && f.Id != "DOCKER_IMAGE_COUNT" && f.Id != "DOCKER_NO_CONTAINERS")
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
            (0, 0) => $"Docker looks healthy. {containers.Count} container(s) found, no issues detected.",
            (var c, 0) => $"{c} critical issue(s) found across {containers.Count} container(s).",
            (0, var w) => $"{w} warning(s) found across {containers.Count} container(s).",
            (var c, var w) => $"{c} critical issue(s) and {w} warning(s) found across {containers.Count} container(s)."
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
