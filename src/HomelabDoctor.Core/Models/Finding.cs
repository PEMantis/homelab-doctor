namespace HomelabDoctor.Core.Models;

public sealed class Finding
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required Severity Severity { get; init; }
    public required string Category { get; init; }
    public required string Evidence { get; init; }
    public required string Explanation { get; init; }
    public string SuggestedFix { get; init; } = string.Empty;
    public IReadOnlyList<string> SuggestedCommands { get; init; } = [];
    public string? DocumentationUrl { get; init; }
}
