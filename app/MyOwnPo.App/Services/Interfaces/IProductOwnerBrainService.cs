namespace MyOwnPo.Services.Interfaces;

public interface IProductOwnerBrainService
{
	Task<string> Chat(string userMessage);
	IAsyncEnumerable<string> ChatStreaming(string userMessage);
}