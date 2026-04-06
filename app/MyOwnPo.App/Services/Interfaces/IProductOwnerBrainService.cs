namespace MyOwnPo.Services.Interfaces;

public interface IProductOwnerBrainService
{
	Task InitializeSession(string sessionId, CancellationToken cancellationToken = default);
	Task<string> Chat(string userMessage);
	IAsyncEnumerable<string> ChatStreaming(string userMessage, CancellationToken cancellationToken = default);
	Task ResetSession(CancellationToken cancellationToken = default);
}