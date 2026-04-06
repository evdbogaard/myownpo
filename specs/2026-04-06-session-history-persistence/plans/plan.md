# Implementation Plan: Session History Persistence

**Created**: 2026-04-06
**Spec**: [specs/2026-04-06-session-history-persistence/spec.md](specs/2026-04-06-session-history-persistence/spec.md)
**Status**: Draft
**Short Name**: session-history-persistence

---

## Planning Intent _(mandatory)_

Implement durable local session history for chat interactions so completed streaming turns are written to disk and restored automatically at startup. This plan uses a service-orchestrated, repository-backed design: `SessionHistoryService` handles behavior, while `FileRepository` handles JSON file access.

**Non-goals for this plan**:

- No cloud persistence or cross-device synchronization.
- No encryption layer for local session files.
- No migration of pre-existing legacy session formats.

---

## Technical Context _(mandatory)_

- **Impacted Areas**: `Services/` (session orchestration), `Services/Interfaces/` (service contracts), `Repositories/` (JSON repository implementation), `Repositories/Interfaces/` (repository contracts), `ConsoleHost` (startup loading and user messaging), `Program.cs` (DI wiring).
- **Existing Services/Repositories/Functions**:
  - `IProductOwnerBrainService` / `ProductOwnerBrainService`: currently streams responses but does not persist turns.
  - `ConsoleHost`: startup currently loads only project context and handles command routing.
  - `ProjectContextService` + `JsonContextFileStore`: existing local JSON persistence pattern to mirror for session persistence.
- **Constraints From Instructions**:
  - `ConsoleHost` (controller-equivalent) calls services, never repositories directly.
  - Services orchestrate business behavior; repositories remain data-access focused.
  - Follow `.editorconfig` and validate with `dotnet format --verify-no-changes`.
  - Keep app usable when save/load operations fail.
- **Dependencies**:
  - `System.Text.Json` for serialization.
  - Host DI container in `Program.cs`.

---

## Scenario Plan Map _(mandatory)_

| Scenario                                      | Priority | Plan File                                                                         | Notes                                                    |
| --------------------------------------------- | -------- | --------------------------------------------------------------------------------- | -------------------------------------------------------- |
| Persist Session After Streaming Interactions  | P1       | [scenario-1.md](specs/2026-04-06-session-history-persistence/plans/scenario-1.md) | Add end-of-stream persistence with failure-safe behavior |
| Resume Conversation After Restart             | P1       | [scenario-2.md](specs/2026-04-06-session-history-persistence/plans/scenario-2.md) | Auto-load session at startup and continue context        |
| Recover Gracefully From Invalid Saved Session | P2       | [scenario-3.md](specs/2026-04-06-session-history-persistence/plans/scenario-3.md) | Handle malformed/unreadable files without blocking usage |

---

## Cross-Scenario Tasks _(when applicable)_

- [ ] Introduce contracts in `Services/Interfaces/` + `Repositories/Interfaces/` and keep runtime history types aligned with Agents Framework types.
- [ ] Add `FileRepository` in `Repositories/` for JSON `LoadFile` / `SaveFile` / `DeleteFile` operations by `fileName`.
- [ ] Add `SessionHistoryService` in `Services/` to coordinate load/save/reset operations using `IFileRepository` and `AgentSession`.
- [ ] Register repository and service dependencies in `Program.cs` with default file path (`session-history.json`).
- [ ] Define session key strategy (`sessionId`, default `default`) mapped to deterministic `fileName` values in `SessionHistoryService`, with load-or-create behavior on missing files.
- [ ] On malformed history, log a warning, delete the invalid file, and create a new session via `_agent.CreateSessionAsync`.
- [ ] Add/extend test fixtures and temp-file helpers for repository and service tests.

---

## Delivery Sequence _(mandatory)_

1. **Slice 1 (P1 foundation)**: Build repository contract/implementation and session service load/save/reset path (Scenario 1).
2. **Slice 2 (P1 continuity)**: Load persisted history at startup and continue conversation context; add intentional reset path (Scenario 2).
3. **Slice 3 (P2 hardening)**: Add malformed-file recovery handling and warning flow, plus fault-path coverage (Scenario 3).

---

## Global Test Strategy _(mandatory)_

### Unit Tests

| Scenario   | Test Project                | Test Class                      | Method Pattern                                                         |
| ---------- | --------------------------- | ------------------------------- | ---------------------------------------------------------------------- |
| Scenario 1 | `app/MyOwnPo.App.UnitTests` | `SessionHistoryServiceTests`    | `SaveSession_Should_PersistFullSessionJsonAfterStreaming`              |
| Scenario 1 | `app/MyOwnPo.App.UnitTests` | `ProductOwnerBrainServiceTests` | `ChatStreaming_Should_PersistSessionAfterCompletedStream`              |
| Scenario 2 | `app/MyOwnPo.App.UnitTests` | `SessionHistoryServiceTests`    | `LoadSession_Should_LoadMessagesInChronologicalOrderWhenSessionExists` |
| Scenario 2 | `app/MyOwnPo.App.UnitTests` | `SessionHistoryServiceTests`    | `ResetSession_Should_CallDeleteFileWhenResettingSession`               |
| Scenario 2 | `app/MyOwnPo.App.UnitTests` | `ConsoleHostTests`              | `Run_Should_LoadSessionBeforePromptWhenHistoryExists`                  |
| Scenario 2 | `app/MyOwnPo.App.UnitTests` | `ConsoleHostTests`              | `HandleInput_Should_ClearPersistedHistoryWhenSessionNewCommand`        |
| Scenario 3 | `app/MyOwnPo.App.UnitTests` | `SessionHistoryServiceTests`    | `LoadSession_Should_DeleteFileAndCreateNewSessionWhenJsonIsMalformed`  |
| Scenario 3 | `app/MyOwnPo.App.UnitTests` | `ConsoleHostTests`              | `Run_Should_DisplayWarningAndContinueWhenSessionFileIsMalformed`       |

---

## Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~SessionHistoryServiceTests|FullyQualifiedName~ProductOwnerBrainServiceTests.ChatStreaming"`
3. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~ConsoleHostTests.Run_Should_LoadSessionBeforePromptWhenHistoryExists|FullyQualifiedName~ConsoleHostTests.Run_Should_DisplayWarningAndContinueWhenSessionFileIsMalformed|FullyQualifiedName~ConsoleHostTests.HandleInput_Should_ClearPersistedHistoryWhenSessionNewCommand"`
4. `dotnet format --verify-no-changes .\myownpo.slnx`
5. Runtime smoke: `dotnet run --project .\app\MyOwnPo.App\MyOwnPo.App.csproj`, send one prompt, exit, restart, verify history restore; then corrupt `session-history.json` and verify warning + usable prompt loop.

---

## Instruction Compliance Checklist _(mandatory)_

- [ ] `ConsoleHost` only coordinates via service interfaces; no direct repository calls in host.
- [ ] Session orchestration remains in `SessionHistoryService` / `ProductOwnerBrainService`; `FileRepository` is data-access only.
- [ ] DI changes are isolated to `Program.cs`.
- [ ] No secrets are persisted in source-controlled files or startup logs.
- [ ] Unit test additions cover normal and failure paths for all scenarios.
- [ ] Style and naming conventions are validated against `.editorconfig` and `dotnet format`.

---

## Unresolved Blockers _(mandatory)_

None.
