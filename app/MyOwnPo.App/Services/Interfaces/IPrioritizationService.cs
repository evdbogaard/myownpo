namespace MyOwnPo.Services.Interfaces;

public interface IPrioritizationService
{
	Task<string> Chat(string userMessage);
}