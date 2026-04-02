namespace MyOwnPo.Models;

public record RoadmapAnalysisResult
{
	public IReadOnlyList<RoadmapStoryLinkRecommendation> LinkedStories { get; init; } = [];
	public IReadOnlyList<RoadmapItem> UnlinkedRoadmapItems { get; init; } = [];
}