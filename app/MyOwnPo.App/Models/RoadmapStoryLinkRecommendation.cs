namespace MyOwnPo.Models;

public record RoadmapStoryLinkRecommendation
{
	public required string RoadmapItemId { get; init; }
	public required string StoryId { get; init; }
	public required string StoryTitle { get; init; }
	public required string Rationale { get; init; }
	public required int ConfidencePercent { get; init; }
}