namespace HomelabDoctor.Checks.Docker.Models;

public enum DockerAvailabilityStatus
{
    Available,
    NotInstalled,
    PermissionDenied,
    DaemonNotRunning,
    Unknown
}

public sealed class DockerAvailability
{
    public required DockerAvailabilityStatus Status { get; init; }
    public string? Message { get; init; }
    public string? ServerVersion { get; init; }
    public int? ContainerCount { get; init; }
    public int? RunningCount { get; init; }
    public int? ImageCount { get; init; }
}
