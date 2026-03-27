namespace MyOwnPo.Gateways;

public interface IWorkItemTrackingClient
{
	Task<IReadOnlyList<IDictionary<string, object?>>> ReadUserStories(
		CancellationToken cancellationToken = default);
}