using Microsoft.Agents.AI;

namespace MyOwnPo.Services.Interfaces;

public interface ISessionHistoryService
{
    Task<AgentSession> LoadSession(string sessionId, CancellationToken cancellationToken = default);
    Task SaveSession(string sessionId, AgentSession session, CancellationToken cancellationToken = default);
    Task<AgentSession> ResetSession(string sessionId, CancellationToken cancellationToken = default);
}