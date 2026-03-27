namespace MyOwnPo.Models;

public record UserStory
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? AcceptanceCriteria { get; init; }
    public int? Priority { get; init; }
    public IReadOnlyList<string> Labels { get; init; } = [];
    public string? Status { get; init; }

    public IReadOnlyList<string> MissingFields =>
        GetMissingFields().ToList();

    private IEnumerable<string> GetMissingFields()
    {
        if (string.IsNullOrWhiteSpace(Description))
            yield return nameof(Description);

        if (string.IsNullOrWhiteSpace(AcceptanceCriteria))
            yield return nameof(AcceptanceCriteria);

        if (Priority is null)
            yield return nameof(Priority);

        if (string.IsNullOrWhiteSpace(Status))
            yield return nameof(Status);
    }
}