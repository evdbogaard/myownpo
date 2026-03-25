using MyOwnPo.Models;

namespace MyOwnPo.Gateways;

public interface IBacklogGateway
{
	Task<IReadOnlyList<UserStory>> ReadStories();
}