# Session History Interfaces

These are the interfaces for the repository-first design, with namespaces and target file locations.

## IFileRepository

Target file: `app/MyOwnPo.App/Repositories/Interfaces/IFileRepository.cs`

```csharp
namespace MyOwnPo.Repositories.Interfaces;

public interface IFileRepository
{
	Task<string?> LoadFile(string fileName, CancellationToken cancellationToken = default);
	Task SaveFile(string fileName, string content, CancellationToken cancellationToken = default);
	Task DeleteFile(string fileName, CancellationToken cancellationToken = default);
}
```

## ISessionHistoryService

Target file: `app/MyOwnPo.App/Services/Interfaces/ISessionHistoryService.cs`

```csharp
using Microsoft.Agents.AI;

namespace MyOwnPo.Services.Interfaces;

public interface ISessionHistoryService
{
	Task<AgentSession> LoadSession(string sessionId, CancellationToken cancellationToken = default);
	Task SaveSession(string sessionId, AgentSession session, CancellationToken cancellationToken = default);
	Task<AgentSession> ResetSession(string sessionId, CancellationToken cancellationToken = default);
}
```

## IProductOwnerBrainService (planned extension)

Target file: `app/MyOwnPo.App/Services/Interfaces/IProductOwnerBrainService.cs`

```csharp
namespace MyOwnPo.Services.Interfaces;

public interface IProductOwnerBrainService
{
	Task InitializeSession(string sessionId, CancellationToken cancellationToken = default);
	IAsyncEnumerable<string> ChatStreaming(string userMessage, CancellationToken cancellationToken = default);
	Task ResetSession(CancellationToken cancellationToken = default);
}
```
