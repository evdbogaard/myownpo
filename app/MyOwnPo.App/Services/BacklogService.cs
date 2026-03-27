using MyOwnPo.Gateways;
using MyOwnPo.Models;

namespace MyOwnPo.Services;

public class BacklogService(IBacklogGateway backlogGateway) : IBacklogService
{
	private const int MaxSupportedStories = 100;
	private readonly IBacklogGateway _backlogGateway = backlogGateway;
	private readonly BacklogConnection _connection = new();
	private IReadOnlyList<UserStory> _stories = [];

	public async Task<IReadOnlyList<UserStory>> Connect()
	{
		var stories = await _backlogGateway.ReadStories();
		EnsureStoryLimit(stories.Count);

		_stories = stories.ToList();
		_connection.IsConnected = true;
		_connection.LastRefreshed = DateTimeOffset.UtcNow;
		_connection.StoryCount = _stories.Count;

		return _stories;
	}

	public IReadOnlyList<UserStory> GetStories() => _stories;

	public async Task<BacklogDiff> Refresh()
	{
		var latestStories = await _backlogGateway.ReadStories();
		EnsureStoryLimit(latestStories.Count);

		var previousById = _stories.ToDictionary(story => story.Id, StringComparer.OrdinalIgnoreCase);
		var latestById = latestStories.ToDictionary(story => story.Id, StringComparer.OrdinalIgnoreCase);

		var added = latestById
			.Where(pair => !previousById.ContainsKey(pair.Key))
			.Select(pair => pair.Value.Title)
			.OrderBy(title => title, StringComparer.OrdinalIgnoreCase)
			.ToList();

		var removed = previousById
			.Where(pair => !latestById.ContainsKey(pair.Key))
			.Select(pair => pair.Value.Title)
			.OrderBy(title => title, StringComparer.OrdinalIgnoreCase)
			.ToList();

		var changed = latestById
			.Where(pair => previousById.TryGetValue(pair.Key, out var previous) && HasChanged(previous, pair.Value))
			.Select(pair => pair.Value.Title)
			.OrderBy(title => title, StringComparer.OrdinalIgnoreCase)
			.ToList();

		_stories = latestStories.ToList();
		_connection.IsConnected = true;
		_connection.LastRefreshed = DateTimeOffset.UtcNow;
		_connection.StoryCount = _stories.Count;

		return new BacklogDiff
		{
			Added = added,
			Removed = removed,
			Changed = changed
		};
	}

	private static bool HasChanged(UserStory previous, UserStory current)
	{
		if (!string.Equals(previous.Title, current.Title, StringComparison.Ordinal)
			|| !string.Equals(previous.Description, current.Description, StringComparison.Ordinal)
			|| !string.Equals(previous.AcceptanceCriteria, current.AcceptanceCriteria, StringComparison.Ordinal)
			|| previous.Priority != current.Priority
			|| !string.Equals(previous.Status, current.Status, StringComparison.Ordinal))
		{
			return true;
		}

		return !LabelsEqual(previous.Labels, current.Labels);
	}

	private static bool LabelsEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
	{
		if (left.Count != right.Count)
			return false;

		var leftSet = new HashSet<string>(left, StringComparer.OrdinalIgnoreCase);
		var rightSet = new HashSet<string>(right, StringComparer.OrdinalIgnoreCase);
		return leftSet.SetEquals(rightSet);
	}

	private static void EnsureStoryLimit(int count)
	{
		if (count > MaxSupportedStories)
			throw new BacklogCapExceededException(count);
	}
}