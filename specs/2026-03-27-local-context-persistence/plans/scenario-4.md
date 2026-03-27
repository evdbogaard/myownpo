# Scenario Plan: Clearing Context Removes the Saved File

**Created**: 2026-03-27
**Spec**: [specs/2026-03-27-local-context-persistence/spec.md](specs/2026-03-27-local-context-persistence/spec.md)
**Master Plan**: [specs/2026-03-27-local-context-persistence/plans/plan.md](specs/2026-03-27-local-context-persistence/plans/plan.md)
**Scenario Priority**: P2
**Scenario Index**: 4

---

## Scenario Goal _(mandatory)_

When the user clears their project context through the application, the local JSON file is removed so that the next startup does not reload stale context. The in-memory state and the persisted state remain consistent.

---

## Acceptance Traceability _(mandatory)_

- **Scenario Acceptance Criteria**:
  1. **Given** the user has a saved context file, **When** they use the clear context command, **Then** the saved file is removed or emptied.
  2. **Given** the user cleared context and restarts the application, **When** the application starts, **Then** no project context is loaded.
- **Related Requirements**: FR-005
- **Related Edge Cases**: File already deleted externally before clear command, file not writable/deletable

---

## Technical Context _(mandatory)_

- **Current Behavior**: `ProjectContextService.ClearContext()` sets `_context = null`. No file interaction.
- **Target Behavior**: `ClearContext()` sets `_context = null` and calls `IContextFileStore.Delete()` to remove the persisted file. If the file was already removed externally, `Delete()` is a no-op. If the file cannot be deleted (permissions), the exception propagates to `ConsoleHost` via the existing error handler.
- **Architecture Boundaries**: `ProjectContextService` calls `IContextFileStore.Delete()`. `ConsoleHost` remains unaware of file operations.
- **Dependencies**: `IContextFileStore` (cross-scenario task, created in Scenario 1).

---

## Implementation Tasks _(mandatory)_

- [ ] **TASK-4-01**: Modify `ProjectContextService.ClearContext` to call `_contextFileStore.Delete()` after setting `_context = null`.
  - **Layer**: Service
  - **Reason**: FR-005 — clearing context removes the saved file.

- [ ] **TASK-4-02**: Verify `JsonContextFileStore.Delete()` handles the case where the file does not exist (no-op, no exception).
  - **Layer**: Service (infrastructure)
  - **Reason**: Edge case — user might clear context when no file exists, or the file was deleted externally.

- [ ] **TASK-4-03**: Add unit tests verifying that `ClearContext` triggers file deletion and that the round-trip (set → clear → restart) results in no context loaded.
  - **Layer**: Tests
  - **Reason**: AC 1 and AC 2 validation.

---

## Files To Alter _(mandatory)_

| File                                                      | Change Type | Why                                                 |
| --------------------------------------------------------- | ----------- | --------------------------------------------------- |
| `app/MyOwnPo.App/Services/ProjectContextService.cs`       | Modify      | Call `_contextFileStore.Delete()` in `ClearContext` |
| `app/MyOwnPo.App.UnitTests/ProjectContextServiceTests.cs` | Modify      | Add tests for clear-deletes-file behavior           |

---

## Technical Questions _(mandatory)_

- **Delete or empty the file?** The spec says "removed or emptied." **Decision**: Delete the file. This is cleaner — an empty file is loaded as no-context anyway (per Scenario 1 handling), but deleting avoids leaving an artifact. `File.Delete` is idempotent if the file doesn't exist (no exception needed).

---

## Testing Criteria _(mandatory)_

### Unit Tests

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `ProjectContextServiceTests`
- **Methods to add**:
  - `ClearContext_WithFile_DeletesFile` — verify `IContextFileStore.Delete` is called
  - `ClearContext_NoFile_DoesNotThrow` — verify clearing when no file exists is safe

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `JsonContextFileStoreTests`
- **Methods to add** (if not already covered in Scenario 1):
  - `Delete_FileExists_RemovesFile`
  - `Delete_FileDoesNotExist_DoesNotThrow`

### Integration Tests

- **Type**: Integration
- **Required**: No — file deletion is fully testable with temp files in unit tests.

---

## Scenario Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~ProjectContextServiceTests.ClearContext|FullyQualifiedName~JsonContextFileStoreTests.Delete"`
3. `dotnet format --verify-no-changes .\myownpo.slnx`
4. Manual smoke: run the app, use `context set` to create a file, use `context clear`, verify the file is gone. Restart, verify no context is loaded.

---

## Scenario Compliance Checks _(mandatory)_

- [ ] No direct file I/O from `ConsoleHost` — `ClearContext` handles deletion internally.
- [ ] Service boundary respected — `ProjectContextService` orchestrates; `JsonContextFileStore` handles file deletion.
- [ ] Security checks and sensitive logging review complete — no secrets; file deletion is limited to the single known context file path.
- [ ] Tests and naming conventions aligned with project standards (xUnit, Moq, `[Method]_[Condition]_[Result]` pattern).
