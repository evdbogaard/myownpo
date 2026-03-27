namespace MyOwnPo.Services;

public interface IPrioritizationService
{
    Task<string> Chat(string userMessage);
}