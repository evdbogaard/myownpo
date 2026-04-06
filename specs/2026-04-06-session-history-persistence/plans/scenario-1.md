# Scenario Plan: Persist Session After Streaming Interactions

**Created**: 2026-04-06
**Spec**: [specs/2026-04-06-session-history-persistence/spec.md](specs/2026-04-06-session-history-persistence/spec.md)
**Master Plan**: [specs/2026-04-06-session-history-persistence/plans/plan.md](specs/2026-04-06-session-history-persistence/plans/plan.md)
**Scenario Priority**: P1
**Scenario Index**: 1

---

## Scenario Goal _(mandatory)_

Persist conversation progress after each completed streaming interaction so recently exchanged messages survive abrupt exits and immediate restarts.

---

## Acceptance Traceability _(mandatory)_

- **Scenario Acceptance Criteria**:
  1. **Given** an active conversation, **When** a streaming interaction completes, **Then** the updated session is persisted automatically.
  2. **Given** a recently persisted interaction, **When** the application restarts, **Then** the newly added messages are present in the restored history.
- **Related Requirements**: FR-001, FR-002, FR-003, FR-004
- **Related Edge Cases**: storage unavailable during save, app closed during save

---

## Technical Context _(mandatory)_

- **Current Behavior**: `ProductOwnerBrainService.ChatStreaming` yields chunks but does not capture a completed assistant message, append user/assistant turns, or write session data to disk.
- **Target Behavior**: after stream completion, the active `AgentSession` is saved in full for the current `sessionId` and persisted immediately.
- **Architecture Boundaries**: `ConsoleHost` remains unaware of file access details; `SessionHistoryService` orchestrates behavior and maps `sessionId` to `fileName`; `FileRepository` handles generic JSON file read/write only.
- **Dependencies**: `System.Text.Json`, DI registrations in `Program.cs`, and Agents Framework session types (`AgentSession`).

---

## Implementation Tasks _(mandatory)_

- [ ] **TASK-1-01**: Define `ISessionHistoryService` contract with `LoadSession`, `SaveSession`, and `ResetSession` methods that take `sessionId` and `AgentSession` where applicable.
  - **Layer**: Service contract
  - **Reason**: align service API with runtime usage and remove message-level append semantics.

- [ ] **TASK-1-02**: Introduce `IFileRepository` in `Repositories/Interfaces` with `LoadFile` / `SaveFile` / `DeleteFile` and implement `FileRepository` in `Repositories` with atomic save behavior.
  - **Layer**: Repository
  - **Reason**: encapsulate JSON data access and reduce partial-write risk (FR-003).

- [ ] **TASK-1-03**: Implement `SessionHistoryService` to map `sessionId` to a deterministic file name, serialize full `AgentSession` state, and call `LoadFile` / `SaveFile` / `DeleteFile`.
  - **Layer**: Service
  - **Reason**: keep orchestration and error handling out of host and repository layers.

- [ ] **TASK-1-04**: Update `ProductOwnerBrainService.ChatStreaming` to save the full active `AgentSession` for the current `sessionId` after streaming completes.
  - **Layer**: Service
  - **Reason**: satisfy FR-001 while ensuring save failure does not break current chat flow (FR-004).

- [ ] **TASK-1-05**: Register `IFileRepository` and `ISessionHistoryService` in `Program.cs` with default file path (`session-history.json`).
  - **Layer**: Composition Root
  - **Reason**: enable runtime wiring and test-time override.

---

## Files To Alter _(mandatory)_

| File                                                            | Change Type | Why                                                |
| --------------------------------------------------------------- | ----------- | -------------------------------------------------- |
| `app/MyOwnPo.App/Repositories/Interfaces/IFileRepository.cs`    | Add         | Repository contract for generic file operations    |
| `app/MyOwnPo.App/Repositories/FileRepository.cs`                | Add         | Local JSON repository implementation               |
| `app/MyOwnPo.App/Services/Interfaces/ISessionHistoryService.cs` | Add         | Service contract for load/save/reset behaviors     |
| `app/MyOwnPo.App/Services/SessionHistoryService.cs`             | Add         | Session orchestration and persistence logic        |
| `app/MyOwnPo.App/Services/ProductOwnerBrainService.cs`          | Modify      | Persist each completed streaming interaction       |
| `app/MyOwnPo.App/Program.cs`                                    | Modify      | DI registrations and default settings              |
| `app/MyOwnPo.App.UnitTests/SessionHistoryServiceTests.cs`       | Add         | Validate load/save behavior and failure resilience |
| `app/MyOwnPo.App.UnitTests/ProductOwnerBrainServiceTests.cs`    | Modify      | Assert stream completion triggers persistence      |

---

## Technical Questions _(mandatory)_

None.

---

## Testing Criteria _(mandatory)_

### Unit Tests

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `SessionHistoryServiceTests`
- **Methods to add/update**:
  - `SaveSession_Should_PersistFullSessionJsonAfterStreaming`
  - `SaveSession_ShouldNot_ThrowWhenSaveFileFails`
  - `LoadSession_Should_RestoreConversationForNextPromptWhenHistoryExists`

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `ProductOwnerBrainServiceTests`
- **Methods to add/update**:
  - `ChatStreaming_Should_PersistSessionAfterCompletedStream`
  - `ChatStreaming_Should_ReturnStreamedTextWhenSaveFails`

---

## Scenario Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~SessionHistoryServiceTests|FullyQualifiedName~ProductOwnerBrainServiceTests.ChatStreaming"`
3. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~ProductOwnerBrainServiceTests"`
4. `dotnet format --verify-no-changes .\myownpo.slnx`
5. Run app and submit one prompt; restart app and verify last user+assistant turn is present in restored session.

---

## Scenario Compliance Checks _(mandatory)_

- [ ] No direct repository calls from `ConsoleHost`.
- [ ] Session orchestration remains in service layer, not repository implementation.
- [ ] Save failures do not terminate the active run.
- [ ] Tests follow `{MethodName}_Should_{ExpectedResult}`, `{MethodName}_ShouldThrow_{ExpectedExceptionAndSituation}`, or `{MethodName}_ShouldNot_{ExpectedCase}` naming convention.
