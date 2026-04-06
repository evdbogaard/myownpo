# Scenario Plan: Resume Conversation After Restart

**Created**: 2026-04-06
**Spec**: [specs/2026-04-06-session-history-persistence/spec.md](specs/2026-04-06-session-history-persistence/spec.md)
**Master Plan**: [specs/2026-04-06-session-history-persistence/plans/plan.md](specs/2026-04-06-session-history-persistence/plans/plan.md)
**Scenario Priority**: P1
**Scenario Index**: 2

---

## Scenario Goal _(mandatory)_

Automatically load previously saved session history at startup and continue the next user interaction with prior context preserved, while also allowing users to intentionally start a fresh conversation.

---

## Acceptance Traceability _(mandatory)_

- **Scenario Acceptance Criteria**:
  1. **Given** a previously saved conversation session, **When** the application starts, **Then** the session is loaded automatically before the user sends a new message.
  2. **Given** a restored session, **When** the user sends a new message, **Then** the conversation continues with prior context preserved.
- **Related Requirements**: FR-005, FR-006, FR-007, FR-009, FR-010
- **Related Edge Cases**: first startup with no file, restore order correctness, user-initiated reset path

---

## Technical Context _(mandatory)_

- **Current Behavior**: the app does not load chat history on startup and each run begins without persisted conversation memory.
- **Target Behavior**: startup flow loads session history by `sessionId` before prompting for input, and subsequent chat calls include restored messages in chronological order.
- **Architecture Boundaries**: startup orchestration remains in `ConsoleHost`; restore logic stays in `SessionHistoryService`; persistence mechanics remain in `FileRepository` via generic file operations.
- **Dependencies**: scenario-1 service/repository contracts, `ConsoleHost` command routing, and session-aware updates in `IProductOwnerBrainService`.

---

## Implementation Tasks _(mandatory)_

- [ ] **TASK-2-01**: Expose `LoadSession(string sessionId)`, `SaveSession(string sessionId, AgentSession session)`, and `ResetSession(string sessionId)` from `ISessionHistoryService`.
  - **Layer**: Service contract
  - **Reason**: provide a minimal service contract that matches runtime session handling without extra getters or status enums.

- [ ] **TASK-2-02**: Update `ProductOwnerBrainService` initialization to consume restored messages for active `sessionId` and seed in-memory conversation state in chronological order.
  - **Layer**: Service
  - **Reason**: ensure `InitializeSession(sessionId)` is the one-time setup step before any `ChatStreaming` call, and that subsequent chat calls reuse the already loaded active session.

- [ ] **TASK-2-02b**: Update `ProductOwnerBrainService` post-stream flow to call `SaveSession(sessionId, activeAgentSession)` so full session state is written after each streaming call.
  - **Layer**: Service
  - **Reason**: ensure persisted data always reflects the complete latest session state.

- [ ] **TASK-2-03**: Update `ConsoleHost.Run` startup sequence to trigger session load before command loop and output restore confirmation when history is found.
  - **Layer**: Console interaction (controller-equivalent)
  - **Reason**: satisfy user-visible startup continuity behavior.

- [ ] **TASK-2-04**: Add user command for intentional reset (for example `session new`) that clears in-memory and persisted history for current `sessionId`.
  - **Layer**: Console interaction + service
  - **Reason**: implement FR-009 and support controlled restart of conversation context; reset should delete persisted session and return a new empty in-memory session without saving it yet.

- [ ] **TASK-2-05**: Update help text and command routing tests to include new session reset command and restored-startup messaging.
  - **Layer**: Console interaction
  - **Reason**: keep UX discoverable and test coverage aligned with behavior changes.

---

## Files To Alter _(mandatory)_

| File                                                               | Change Type | Why                                                                                           |
| ------------------------------------------------------------------ | ----------- | --------------------------------------------------------------------------------------------- |
| `app/MyOwnPo.App/Services/Interfaces/ISessionHistoryService.cs`    | Modify      | Add startup load/save/reset APIs keyed by `sessionId`                                         |
| `app/MyOwnPo.App/Services/SessionHistoryService.cs`                | Modify      | Implement restore/read/reset orchestration                                                    |
| `app/MyOwnPo.App/Services/Interfaces/IProductOwnerBrainService.cs` | Modify      | Ensure `ChatStreaming` uses active initialized session instead of taking `sessionId` per call |
| `app/MyOwnPo.App/Services/ProductOwnerBrainService.cs`             | Modify      | Apply restored history to ongoing chat context                                                |
| `app/MyOwnPo.App/Repositories/Interfaces/IFileRepository.cs`       | Modify      | Expose `LoadFile` / `SaveFile` / `DeleteFile` with `fileName`                                 |
| `app/MyOwnPo.App/Repositories/FileRepository.cs`                   | Modify      | Implement generic file operations without session semantics                                   |
| `app/MyOwnPo.App/ConsoleHost.cs`                                   | Modify      | Startup load call, restore messaging, `session new` command                                   |
| `app/MyOwnPo.App/Program.cs`                                       | Modify      | Ensure startup wiring includes repository and service dependencies                            |
| `app/MyOwnPo.App.UnitTests/SessionHistoryServiceTests.cs`          | Modify      | Validate startup restore and reset behavior                                                   |
| `app/MyOwnPo.App.UnitTests/ProductOwnerBrainServiceTests.cs`       | Modify      | Validate continuation with restored context                                                   |
| `app/MyOwnPo.App.UnitTests/ConsoleHostTests.cs`                    | Modify      | Validate startup restore output and reset command flow                                        |

---

## Technical Questions _(mandatory)_

None. Use the default session name for this feature slice; session switching is out of scope for now.

---

## Testing Criteria _(mandatory)_

### Unit Tests

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `SessionHistoryServiceTests`
- **Methods to add/update**:
  - `LoadSession_Should_LoadMessagesInChronologicalOrderWhenSessionExists`
  - `LoadSession_Should_CreateNewSessionWhenHistoryFileIsMissing`
  - `ResetSession_Should_CallDeleteFileWhenResettingSession`
  - `ResetSession_Should_ClearInMemoryStateAndDeletePersistedFile`

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `ProductOwnerBrainServiceTests`
- **Methods to add/update**:
  - `ChatStreaming_Should_UseRestoredContextAfterInitializeSession`
  - `InitializeSession_Should_SeedConversationStateWhenHistoryExists`

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `ConsoleHostTests`
- **Methods to add/update**:
  - `Run_Should_LoadSessionBeforePromptWhenHistoryExists`
  - `HandleInput_Should_ClearPersistedHistoryWhenSessionNewCommand`
  - `HandleInput_Should_IncludeSessionNewCommandInHelpOutput`

---

## Scenario Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~SessionHistoryServiceTests.LoadSession|FullyQualifiedName~SessionHistoryServiceTests.ResetSession"`
3. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~ConsoleHostTests.Run_Should_LoadSessionBeforePromptWhenHistoryExists|FullyQualifiedName~ConsoleHostTests.HandleInput_Should_ClearPersistedHistoryWhenSessionNewCommand|FullyQualifiedName~ProductOwnerBrainServiceTests.InitializeSession_Should_SeedConversationStateWhenHistoryExists"`
4. `dotnet format --verify-no-changes .\myownpo.slnx`
5. Run app, verify startup reports restored history, send a follow-up question, and confirm response reflects prior context; then run `session new` and verify next prompt starts clean.

---

## Scenario Compliance Checks _(mandatory)_

- [ ] Host startup and command handling call service interfaces only.
- [ ] Restore and reset orchestration remains in service layer; repository stays data access only.
- [ ] Message order is preserved exactly as persisted.
- [ ] Tests and naming follow existing project conventions.
