# Scenario Plan: Context Automatically Saved When Set

**Created**: 2026-03-27
**Spec**: [specs/2026-03-27-local-context-persistence/spec.md](specs/2026-03-27-local-context-persistence/spec.md)
**Master Plan**: [specs/2026-03-27-local-context-persistence/plans/plan.md](specs/2026-03-27-local-context-persistence/plans/plan.md)
**Scenario Priority**: P1
**Scenario Index**: 2

---

## Scenario Goal _(mandatory)_

When the user sets or updates project context through the application, the context is automatically persisted to a local JSON file so it will be available in future sessions. No explicit "save" action is needed — persistence is seamless.

---

## Acceptance Traceability _(mandatory)_

- **Scenario Acceptance Criteria**:
  1. **Given** the user sets project context through the application, **When** the context is saved, **Then** a JSON file is created locally containing all provided context fields.
  2. **Given** a context file already exists, **When** the user sets new context values, **Then** the file is updated to reflect the latest values.
  3. **Given** the user sets context with some fields left empty, **When** the file is saved, **Then** only populated fields appear in the file (empty fields are omitted or shown as blank).
- **Related Requirements**: FR-001, FR-003, FR-010
- **Related Edge Cases**: File location not writable (permission issue), directory doesn't exist yet

---

## Technical Context _(mandatory)_

- **Current Behavior**: `ProjectContextService.SetContext` stores the context in memory only (`_context = context`). `UpdateContext` applies a mutation to the in-memory context. Neither method persists anything.
- **Target Behavior**: After `SetContext` or `UpdateContext` updates the in-memory state, the service calls `IContextFileStore.Save(_context)` to persist the current context to disk. If the save fails (e.g., permission issue), the in-memory context is retained and an error is surfaced to the caller.
- **Architecture Boundaries**: `ProjectContextService` calls `IContextFileStore.Save()`. `ConsoleHost` is unaware of persistence — it continues calling `SetContext` as before.
- **Dependencies**: `IContextFileStore` (cross-scenario task, created in Scenario 1).

---

## Implementation Tasks _(mandatory)_

- [ ] **TASK-2-01**: Modify `ProjectContextService.SetContext` to call `_contextFileStore.Save(context)` after setting `_context`.
  - **Layer**: Service
  - **Reason**: FR-001 — automatic save on set. Constructor injection of `IContextFileStore` is handled in Scenario 1 (TASK-1-02b).

- [ ] **TASK-2-02**: Modify `ProjectContextService.UpdateContext` to call `_contextFileStore.Save(_context)` after applying the update.
  - **Layer**: Service
  - **Reason**: FR-001 — automatic save on update.

- [ ] **TASK-2-03**: In `JsonContextFileStore.Save`, use `JsonSerializerOptions` with `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` so that null/empty fields are omitted from the saved JSON (AC 3).
  - **Layer**: Service (infrastructure)
  - **Reason**: AC 3 requires that empty fields are omitted or shown as blank.

- [ ] **TASK-2-04**: Handle save failures gracefully — if `Save` throws an `IOException`, the in-memory context is still retained. The exception should propagate to `ConsoleHost` where it is caught by the existing `catch (Exception ex)` handler displayed as `"Operation failed: {ex.Message}"`.
  - **Layer**: Service / Console Interaction
  - **Reason**: FR-009 — clear error message on file system issues, without losing in-session context.

---

## Files To Alter _(mandatory)_

| File                                                      | Change Type | Why                                                                                                         |
| --------------------------------------------------------- | ----------- | ----------------------------------------------------------------------------------------------------------- |
| `app/MyOwnPo.App/Services/ProjectContextService.cs`       | Modify      | Call `Save` in `SetContext` and `UpdateContext` (constructor injection handled in Scenario 1 TASK-1-02b)    |
| `app/MyOwnPo.App/Services/JsonContextFileStore.cs`        | Modify      | Configure `DefaultIgnoreCondition` for null fields                                                          |
| `app/MyOwnPo.App.UnitTests/ProjectContextServiceTests.cs` | Modify      | Add tests verifying save is called on set/update; update existing tests to provide mock `IContextFileStore` |
| `app/MyOwnPo.App.UnitTests/JsonContextFileStoreTests.cs`  | Modify      | Add test for null-field omission in saved JSON                                                              |

---

## Technical Questions _(mandatory)_

- **Should save failures be silent or loud?** **Decision**: Loud — the exception propagates to `ConsoleHost` and is displayed via the existing error handler. The in-memory context is retained regardless. This matches FR-009.
- **Should `UpdateContext` save even if no fields actually changed?** **Decision**: Yes — always save after update. The cost is negligible (small JSON file) and the complexity of change detection isn't justified.

---

## Testing Criteria _(mandatory)_

### Unit Tests

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `ProjectContextServiceTests`
- **Methods to add/update**:
  - `SetContext_ValidContext_PersistsToFile` — verify `IContextFileStore.Save` is called with the context
  - `UpdateContext_ExistingContext_PersistsToFile` — verify `IContextFileStore.Save` is called after update
  - `SetContext_SaveThrows_RetainsInMemoryContext` — verify context is still accessible after save failure
  - (Existing tests must be updated to provide a mock `IContextFileStore` to the constructor)

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `JsonContextFileStoreTests`
- **Methods to add**:
  - `Save_ContextWithNullFields_OmitsNullFieldsFromJson` — verify null fields are not present in the JSON output
  - `Save_OverwritesExistingFile` — verify saving to an existing path replaces the content

### Integration Tests

- **Type**: Integration
- **Required**: No — file I/O fully testable with temp files in unit tests.

---

## Scenario Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~ProjectContextServiceTests.SetContext|FullyQualifiedName~ProjectContextServiceTests.UpdateContext|FullyQualifiedName~JsonContextFileStoreTests.Save"`
3. `dotnet format --verify-no-changes .\myownpo.slnx`
4. Manual smoke: run the app, use `context set` to provide values, exit, verify `project-context.json` exists with correct content. Restart, use `context set` with new values, verify file is updated.

---

## Scenario Compliance Checks _(mandatory)_

- [ ] No direct file I/O from `ConsoleHost` — `SetContext` handles persistence internally.
- [ ] Service boundary respected — `ProjectContextService` orchestrates the save; `JsonContextFileStore` handles file I/O only.
- [ ] Security checks and sensitive logging review complete — no secrets; context fields are user-provided project information.
- [ ] Tests and naming conventions aligned with project standards (xUnit, Moq, `[Method]_[Condition]_[Result]` pattern).

---

## Revisions

### Session 2026-03-27

- **C1** (Consistency / HIGH): TASK-2-01 (constructor injection) moved to scenario-1 where the dependency is first needed → **Fix**: Removed TASK-2-01; renumbered remaining tasks (TASK-2-01 is now SetContext save, TASK-2-02 is UpdateContext save, etc.); added cross-reference to scenario-1 TASK-1-02b
