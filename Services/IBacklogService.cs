using MyOwnPo.Models;

namespace MyOwnPo.Services;

public interface IBacklogService
{
	Task<IReadOnlyList<UserStory>> Connect();
	IReadOnlyList<UserStory> GetStories();
	Task<BacklogDiff> Refresh();
}