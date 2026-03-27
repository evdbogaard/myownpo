namespace MyOwnPo.Models;

public record PrioritizationSuggestion
{
	public required IReadOnlyList<SuggestedStoryRank> Rankings { get; init; }
	public required DateTimeOffset Created { get; init; }
	public IReadOnlyList<string> DuplicateWarnings { get; init; } = [];
}