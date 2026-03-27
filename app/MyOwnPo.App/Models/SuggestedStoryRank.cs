namespace MyOwnPo.Models;

public record SuggestedStoryRank
{
    public required string StoryId { get; init; }
    public required string StoryTitle { get; init; }
    public required int Rank { get; init; }
    public required string Justification { get; init; }
}