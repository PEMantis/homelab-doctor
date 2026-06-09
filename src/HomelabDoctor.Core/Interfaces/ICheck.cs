using HomelabDoctor.Core.Models;

namespace HomelabDoctor.Core.Interfaces;

public interface ICheck
{
    string Name { get; }
    Task<CheckResult> RunAsync(CancellationToken ct = default);
}
