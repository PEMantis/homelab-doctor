using HomelabDoctor.Core.Models;
using Xunit;

namespace HomelabDoctor.Core.Tests;

public class FindingSortingTests
{
    [Fact]
    public void Findings_sort_critical_before_warning_before_info()
    {
        var findings = new List<Finding>
        {
            MakeFinding("info1", Severity.Info),
            MakeFinding("critical1", Severity.Critical),
            MakeFinding("warning1", Severity.Warning),
            MakeFinding("info2", Severity.Info),
            MakeFinding("critical2", Severity.Critical)
        };

        var sorted = findings.OrderByDescending(f => f.Severity).ToList();

        Assert.Equal(Severity.Critical, sorted[0].Severity);
        Assert.Equal(Severity.Critical, sorted[1].Severity);
        Assert.Equal(Severity.Warning, sorted[2].Severity);
        Assert.Equal(Severity.Info, sorted[3].Severity);
        Assert.Equal(Severity.Info, sorted[4].Severity);
    }

    [Theory]
    [InlineData(Severity.Info, 0)]
    [InlineData(Severity.Warning, 1)]
    [InlineData(Severity.Critical, 2)]
    public void Severity_enum_values_have_expected_ordinal(Severity severity, int expected)
    {
        Assert.Equal(expected, (int)severity);
    }

    private static Finding MakeFinding(string id, Severity severity) => new()
    {
        Id = id,
        Title = id,
        Severity = severity,
        Category = "Test",
        Evidence = "n/a",
        Explanation = "n/a"
    };
}
