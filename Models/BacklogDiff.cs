namespace MyOwnPo.Models;

public record BacklogDiff
{
	public required IReadOnlyList<string> Added { get; init; }
	public required IReadOnlyList<string> Removed { get; init; }
	public required IReadOnlyList<string> Changed { get; init; }

	public bool HasChanges => Added.Count > 0 || Removed.Count > 0 || Changed.Count > 0;
}