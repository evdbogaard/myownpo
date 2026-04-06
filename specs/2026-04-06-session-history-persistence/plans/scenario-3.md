# Scenario Plan: Recover Gracefully From Invalid Saved Session

**Created**: 2026-04-06
**Spec**: [specs/2026-04-06-session-history-persistence/spec.md](specs/2026-04-06-session-history-persistence/spec.md)
**Master Plan**: [specs/2026-04-06-session-history-persistence/plans/plan.md](specs/2026-04-06-session-history-persistence/plans/plan.md)
**Scenario Priority**: P2
**Scenario Index**: 3

---

## Scenario Goal _(mandatory)_

Ensure startup remains usable when the saved session file is unreadable or malformed by falling back to a clean session and notifying the user clearly.

---

## Acceptance Traceability _(mandatory)_

- **Scenario Acceptance Criteria**:
  1. **Given** a saved session that cannot be restored, **When** the application starts, **Then** the application starts a new session without crashing.
  2. **Given** restore failure, **When** startup completes, **Then** the user is informed that historical context could not be loaded.
- **Related Requirements**: FR-004, FR-008
- **Related Edge Cases**: empty file, truncated file, malformed JSON, read permission errors

---

## Technical Context _(mandatory)_

- **Current Behavior**: no session history load path exists, so malformed-session handling is not implemented.
- **Target Behavior**: if history content is malformed, startup writes a warning log line, deletes the invalid history file, creates a new empty session via `_agent.CreateSessionAsync`, and continues.
- **Architecture Boundaries**: `FileRepository` reports load failures, `SessionHistoryService` handles malformed recovery (log/delete/recreate), and `ConsoleHost` remains usable.
- **Dependencies**: startup load behavior from scenario 2 and repository implementation from scenario 1.

---

## Implementation Tasks _(mandatory)_

- [ ] **TASK-3-01**: Extend `FileRepository.LoadFile(fileName)` to distinguish missing file from invalid content by throwing structured exceptions for malformed/unreadable content.
  - **Layer**: Repository
  - **Reason**: enable accurate error classification for user feedback.

- [ ] **TASK-3-02**: Implement robust error handling in `SessionHistoryService.LoadSession(sessionId)` that logs malformed parse/read failures, deletes the invalid history file, and creates a clean in-memory session with `_agent.CreateSessionAsync`.
  - **Layer**: Service
  - **Reason**: enforce FR-004 by preventing startup crashes.

- [ ] **TASK-3-03**: Ensure startup flow in `ConsoleHost.Run` continues normally after service-level malformed recovery; no crash, with warning logging performed by the service.
  - **Layer**: Console interaction
  - **Reason**: keep host resilient while logging responsibility stays in session service.

- [ ] **TASK-3-04**: Ensure save operations after fallback overwrite invalid state with valid session JSON once the next streaming interaction completes.
  - **Layer**: Service orchestration
  - **Reason**: enable automatic recovery on subsequent successful save.

---

## Files To Alter _(mandatory)_

| File                                                      | Change Type | Why                                                   |
| --------------------------------------------------------- | ----------- | ----------------------------------------------------- |
| `app/MyOwnPo.App/Repositories/FileRepository.cs`          | Modify      | Add malformed/unreadable behavior for `LoadFile`      |
| `app/MyOwnPo.App/Services/SessionHistoryService.cs`       | Modify      | Translate load failures into non-fatal startup result |
| `app/MyOwnPo.App/ConsoleHost.cs`                          | Modify      | Display warning and continue on malformed load        |
| `app/MyOwnPo.App.UnitTests/SessionHistoryServiceTests.cs` | Modify      | Add malformed-load fallback tests                     |
| `app/MyOwnPo.App.UnitTests/ConsoleHostTests.cs`           | Modify      | Verify warning message and startup usability          |

---

## Technical Questions _(mandatory)_

None. For now, malformed session history files are deleted directly (no backup/rename flow).

---

## Testing Criteria _(mandatory)_

### Unit Tests

- **Type**: Unit
- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `SessionHistoryServiceTests`
- **Methods to add/update**:
  - `LoadSession_Should_DeleteFileAndCreateNewSessionWhenJsonIsMalformed`
  - `LoadSession_Should_DeleteFileAndCreateNewSessionWhenReadFails`
  - `SaveSession_Should_RewriteValidSessionFileAfterMalformedLoad`

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `ConsoleHostTests`
- **Methods to add/update**:
  - `Run_Should_DisplayWarningAndContinueWhenSessionFileIsMalformed`
  - `Run_Should_AllowNewPromptAfterMalformedSessionWarning`

---

## Scenario Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~SessionHistoryServiceTests.LoadSession_Should_DeleteFileAndCreateNewSessionWhenJsonIsMalformed|FullyQualifiedName~SessionHistoryServiceTests.LoadSession_Should_DeleteFileAndCreateNewSessionWhenReadFails|FullyQualifiedName~SessionHistoryServiceTests.SaveSession_Should_RewriteValidSessionFileAfterMalformedLoad"`
3. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~ConsoleHostTests.Run_Should_DisplayWarningAndContinueWhenSessionFileIsMalformed|FullyQualifiedName~ConsoleHostTests.Run_Should_AllowNewPromptAfterMalformedSessionWarning"`
4. `dotnet format --verify-no-changes .\myownpo.slnx`
5. Manually place malformed content in `session-history.json`, run app, verify warning message appears and interactive chat still works.

---

## Scenario Compliance Checks _(mandatory)_

- [ ] Startup load failures are handled without terminating the process.
- [ ] User receives a clear warning when history restoration fails.
- [ ] No repository implementation details leak into `ConsoleHost`.
- [ ] Test coverage includes malformed JSON and unreadable file paths.
