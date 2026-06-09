using HomelabDoctor.Checks.Docker;
using HomelabDoctor.Checks.Docker.Models;
using HomelabDoctor.Core.Models;
using NSubstitute;
using Xunit;

namespace HomelabDoctor.Checks.Docker.Tests;

public class DockerCheckTests
{
    private static DockerAvailability Available(string version = "24.0.0") => new()
    {
        Status = DockerAvailabilityStatus.Available,
        ServerVersion = version,
        ContainerCount = 0,
        RunningCount = 0,
        ImageCount = 0
    };

    [Fact]
    public async Task Returns_unavailable_when_docker_not_installed()
    {
        var inspector = Substitute.For<IDockerInspector>();
        inspector.GetAvailabilityAsync(default).ReturnsForAnyArgs(
            new DockerAvailability { Status = DockerAvailabilityStatus.NotInstalled, Message = "not found" });

        var check = new DockerCheck(inspector);
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Unavailable, result.Status);
        Assert.Contains(result.Findings, f => f.Id == "UNAVAILABLE");
    }

    [Fact]
    public async Task Returns_unavailable_when_permission_denied()
    {
        var inspector = Substitute.For<IDockerInspector>();
        inspector.GetAvailabilityAsync(default).ReturnsForAnyArgs(
            new DockerAvailability
            {
                Status = DockerAvailabilityStatus.PermissionDenied,
                Message = "Permission denied when connecting to Docker daemon."
            });

        var check = new DockerCheck(inspector);
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Unavailable, result.Status);
        Assert.Contains("Permission", result.Summary);
    }

    [Fact]
    public async Task Returns_unavailable_when_daemon_not_running()
    {
        var inspector = Substitute.For<IDockerInspector>();
        inspector.GetAvailabilityAsync(default).ReturnsForAnyArgs(
            new DockerAvailability
            {
                Status = DockerAvailabilityStatus.DaemonNotRunning,
                Message = "Docker daemon is not running."
            });

        var check = new DockerCheck(inspector);
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Produces_critical_finding_for_restarting_container()
    {
        var inspector = Substitute.For<IDockerInspector>();
        inspector.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        inspector.GetContainersAsync(default).ReturnsForAnyArgs(
        [
            new ContainerSummary
            {
                Id = "abc123",
                Name = "my-service",
                Image = "myapp:latest",
                State = "restarting",
                Status = "Restarting (1) 3 seconds ago",
                RestartCount = 14,
                HasHealthcheck = false,
                HealthStatus = null,
                PublicPorts = []
            }
        ]);

        var check = new DockerCheck(inspector);
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Critical, result.Status);
        var finding = result.Findings.Single(f => f.Id.StartsWith("DOCKER_RESTARTING_"));
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Contains("my-service", finding.Title);
        Assert.Contains("14", finding.Evidence);
        Assert.Contains("docker logs --tail=100 my-service", finding.SuggestedCommands);
    }

    [Fact]
    public async Task Produces_warning_for_container_missing_healthcheck()
    {
        var inspector = Substitute.For<IDockerInspector>();
        inspector.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        inspector.GetContainersAsync(default).ReturnsForAnyArgs(
        [
            new ContainerSummary
            {
                Id = "abc123",
                Name = "no-health",
                Image = "nginx:alpine",
                State = "running",
                Status = "Up 2 hours",
                RestartCount = 0,
                HasHealthcheck = false,
                HealthStatus = null,
                PublicPorts = []
            }
        ]);

        var check = new DockerCheck(inspector);
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Warning, result.Status);
        var finding = result.Findings.Single(f => f.Id == "DOCKER_NO_HEALTHCHECK");
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("no-health", finding.Evidence);
    }

    [Fact]
    public async Task Produces_warning_for_container_exposing_port_on_all_interfaces()
    {
        var inspector = Substitute.For<IDockerInspector>();
        inspector.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        inspector.GetContainersAsync(default).ReturnsForAnyArgs(
        [
            new ContainerSummary
            {
                Id = "abc123",
                Name = "exposed-svc",
                Image = "redis:7",
                State = "running",
                Status = "Up 1 hour",
                RestartCount = 0,
                HasHealthcheck = true,
                HealthStatus = "healthy",
                PublicPorts = ["0.0.0.0:6379->6379/tcp"]
            }
        ]);

        var check = new DockerCheck(inspector);
        var result = await check.RunAsync();

        var finding = result.Findings.Single(f => f.Id == "DOCKER_EXPOSED_PORTS");
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("exposed-svc", finding.Evidence);
        Assert.Contains("0.0.0.0:6379", finding.Evidence);
    }

    [Fact]
    public async Task Returns_passed_when_all_containers_healthy()
    {
        var inspector = Substitute.For<IDockerInspector>();
        inspector.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        inspector.GetContainersAsync(default).ReturnsForAnyArgs(
        [
            new ContainerSummary
            {
                Id = "abc123",
                Name = "healthy-app",
                Image = "myapp:1.0",
                State = "running",
                Status = "Up 3 days (healthy)",
                RestartCount = 0,
                HasHealthcheck = true,
                HealthStatus = "healthy",
                PublicPorts = []
            }
        ]);

        var check = new DockerCheck(inspector);
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.DoesNotContain(result.Findings, f => f.Severity >= Severity.Warning);
    }

    [Fact]
    public async Task Produces_critical_finding_for_unhealthy_container()
    {
        var inspector = Substitute.For<IDockerInspector>();
        inspector.GetAvailabilityAsync(default).ReturnsForAnyArgs(Available());
        inspector.GetContainersAsync(default).ReturnsForAnyArgs(
        [
            new ContainerSummary
            {
                Id = "abc123",
                Name = "sick-service",
                Image = "myapp:latest",
                State = "running",
                Status = "Up 2 hours (unhealthy)",
                RestartCount = 0,
                HasHealthcheck = true,
                HealthStatus = "unhealthy",
                PublicPorts = []
            }
        ]);

        var check = new DockerCheck(inspector);
        var result = await check.RunAsync();

        Assert.Equal(CheckStatus.Critical, result.Status);
        var finding = result.Findings.Single(f => f.Id.StartsWith("DOCKER_UNHEALTHY_"));
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Contains("sick-service", finding.Title);
    }
}
