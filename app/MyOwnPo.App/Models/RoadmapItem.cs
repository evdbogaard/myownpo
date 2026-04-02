namespace MyOwnPo.Models;

public record RoadmapItem
{
	public required string Id { get; init; }
	public required string Title { get; init; }
	public string? Description { get; init; }
	public string? TimeHorizon { get; init; }
	public IReadOnlyList<string> Tags { get; init; } = [];
}