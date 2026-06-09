using HomelabDoctor.Core.Models;

namespace HomelabDoctor.Core.Interfaces;

public interface IReportRenderer
{
    void Render(IReadOnlyList<CheckResult> results);
    string RenderToString(IReadOnlyList<CheckResult> results);
}
