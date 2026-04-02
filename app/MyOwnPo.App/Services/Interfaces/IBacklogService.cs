using MyOwnPo.Models;

namespace MyOwnPo.Services.Interfaces;

public interface IBacklogService
{
	Task<IReadOnlyList<UserStory>> Connect();
	IReadOnlyList<UserStory> GetStories();
	Task<BacklogDiff> Refresh();
}