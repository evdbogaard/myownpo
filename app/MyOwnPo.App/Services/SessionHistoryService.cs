using System.Text.Json;

using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MyOwnPo.App.Agents;
using MyOwnPo.Repositories.Interfaces;
using MyOwnPo.Services.Interfaces;

namespace MyOwnPo.Services;

public class SessionHistoryService(
    IFileRepository fileRepository,
    [FromKeyedServices(POAgentHelper.AgentName)] AIAgent agent,
    ILogger<SessionHistoryService> logger) : ISessionHistoryService
{
    private const string DefaultSessionId = "default";

    private readonly IFileRepository _fileRepository = fileRepository;
    private readonly AIAgent _agent = agent;
    private readonly ILogger<SessionHistoryService> _logger = logger;

    public async Task<AgentSession> LoadSession(string sessionId, CancellationToken cancellationToken = default)
    {
        var fileName = $"{sessionId}.json";
        string? serializedSession;
        try
        {
            serializedSession = await _fileRepository.LoadFile(fileName, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not read session history file '{FileName}'. Starting a new session.", fileName);
            return await _agent.CreateSessionAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(serializedSession))
            return await _agent.CreateSessionAsync(cancellationToken);

        try
        {
            using var document = JsonDocument.Parse(serializedSession);
            var state = document.RootElement.Clone();
            return await _agent.DeserializeSessionAsync(state, jsonSerializerOptions: null, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            _logger.LogWarning(ex, "Could not restore session history from '{FileName}'. Starting a new session.", fileName);
            await _fileRepository.DeleteFile(fileName, cancellationToken);
            return await _agent.CreateSessionAsync(cancellationToken);
        }
    }

    public async Task SaveSession(string sessionId, AgentSession session, CancellationToken cancellationToken = default)
    {
        var fileName = $"{sessionId}.json";

        try
        {
            var state = await _agent.SerializeSessionAsync(session, cancellationToken: cancellationToken);
            var json = JsonSerializer.Serialize(state);
            await _fileRepository.SaveFile(fileName, json, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            _logger.LogWarning(ex, "Could not persist session history to '{FileName}'. Continuing without persistence.", fileName);
        }
    }

    public async Task<AgentSession> ResetSession(string sessionId, CancellationToken cancellationToken = default)
    {
        var fileName = $"{sessionId}.json";
        await _fileRepository.DeleteFile(fileName, cancellationToken);
        return await _agent.CreateSessionAsync(cancellationToken);
    }
}