using HomelabDoctor.Checks.Docker.Models;

namespace HomelabDoctor.Checks.Docker;

public interface IDockerInspector
{
    Task<DockerAvailability> GetAvailabilityAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ContainerSummary>> GetContainersAsync(CancellationToken ct = default);
}
