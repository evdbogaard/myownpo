# Implementation Plan: Local Project Context Persistence

**Created**: 2026-03-27
**Spec**: [specs/2026-03-27-local-context-persistence/spec.md](specs/2026-03-27-local-context-persistence/spec.md)
**Status**: Accepted
**Short Name**: local-context-persistence

---

## Planning Intent _(mandatory)_

Add local JSON file persistence to the existing `ProjectContextService` so that project context survives between application sessions. The context is automatically saved when set and automatically loaded on startup. Clearing context removes the persisted file. The JSON file is human-readable and externally editable.

**Non-goals for this plan**:

- No cloud or remote storage — local file only.
- No file-watching or hot-reload — changes are picked up on next startup only.
- No encryption or access control beyond the OS file system.
- No migration from other formats — JSON is the only supported format.

---

## Technical Context _(mandatory)_

- **Impacted Areas**: `Services/` (persistence logic), `ConsoleHost` (startup load + user notifications), `Program.cs` (DI wiring for file path/abstraction), `Models/` (no changes expected — `ProjectContext` already has the required properties).
- **Existing Services/Repositories/Functions**:
  - `IProjectContextService` / `ProjectContextService` — in-memory context store with `SetContext`, `GetContext`, `ClearContext`, `UpdateContext`, `HasContext`.
  - `ConsoleHost` — handles `context set`, `context show`, `context clear` commands. Calls `IProjectContextService`.
  - `Program.cs` — composition root; registers `ProjectContextService` as singleton.
- **Constraints From Instructions**:
  - `ConsoleHost` (controller-equivalent) must call services, never file I/O directly.
  - Services orchestrate business logic; file I/O abstracted behind an interface for testability.
  - Tab indentation, 4-space tab width (per `.editorconfig`).
  - `dotnet format --verify-no-changes` must pass.
  - Nullable reference types enabled throughout.
- **Dependencies**: No new NuGet packages required. `System.Text.Json` is available via the implicit framework reference.

---

## Scenario Plan Map _(mandatory)_

| Scenario                                  | Priority | Plan File                                                                       | Notes                                               |
| ----------------------------------------- | -------- | ------------------------------------------------------------------------------- | --------------------------------------------------- |
| Resume Work With Previously Saved Context | P1       | [scenario-1.md](specs/2026-03-27-local-context-persistence/plans/scenario-1.md) | Load context from JSON file on startup; notify user |
| Context Automatically Saved When Set      | P1       | [scenario-2.md](specs/2026-03-27-local-context-persistence/plans/scenario-2.md) | Persist context to JSON file on every set/update    |
| Edit Context File Externally              | P2       | [scenario-3.md](specs/2026-03-27-local-context-persistence/plans/scenario-3.md) | Externally edited JSON files load correctly         |
| Clearing Context Removes the Saved File   | P2       | [scenario-4.md](specs/2026-03-27-local-context-persistence/plans/scenario-4.md) | Clear command deletes persisted file                |

---

## Cross-Scenario Tasks _(when applicable)_

- [ ] **Introduce `IContextFileStore` interface** with methods: `ProjectContext? Load()`, `void Save(ProjectContext context)`, `void Delete()`. This abstracts file I/O for testability across all scenarios.
- [ ] **Implement `JsonContextFileStore : IContextFileStore`** — reads/writes a JSON file at a configurable path using `System.Text.Json`. Handles missing file, empty file, malformed JSON, partial fields, and unrecognized properties gracefully.
- [ ] **Extend `ProjectContextService`** to accept `IContextFileStore` via constructor injection. `SetContext` and `UpdateContext` call `Save` after updating in-memory state. `ClearContext` calls `Delete`. A new `LoadFromFile` method (or startup-specific method) reads from the store and populates in-memory state.
- [ ] **Register `IContextFileStore` / `JsonContextFileStore`** in `Program.cs` with a configurable file path (default: `project-context.json` in the current working directory).
- [ ] **Update `ConsoleHost.Run`** to attempt context load on startup and display appropriate messages (loaded confirmation, warning on malformed file, silence on missing file).

---

## Delivery Sequence _(mandatory)_

1. **Slice 1 — File Store Abstraction + Save on Set (Scenarios 1 & 2 foundation)**
   Create `IContextFileStore` and `JsonContextFileStore`. Extend `ProjectContextService` to persist on `SetContext`/`UpdateContext`. Wire up DI in `Program.cs`. Unit tests for `JsonContextFileStore` and updated `ProjectContextService`.

2. **Slice 2 — Load on Startup + User Notification (Scenario 1 completion)**
   Add `LoadFromFile` to `ProjectContextService`. Update `ConsoleHost.Run` to call load on startup and display confirmation or warning. Unit tests for load behavior and `ConsoleHost` startup messages.

3. **Slice 3 — Clear Deletes File + External Edit Support (Scenarios 3 & 4)**
   Extend `ClearContext` to call `Delete` on the file store. Verify externally edited files load correctly (covered by load tests with hand-crafted JSON). Unit tests for delete behavior and edge cases (partial fields, extra properties, empty file, malformed JSON).

---

## Global Test Strategy _(mandatory)_

### Unit Tests

| Scenario   | Test Project                | Test Class                   | Method Pattern                                                                                                                                                                                                     |
| ---------- | --------------------------- | ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Scenario 1 | `app/MyOwnPo.App.UnitTests` | `JsonContextFileStoreTests`  | `Save_ValidContext_WritesJsonFile`, `Save_DirectoryDoesNotExist_CreatesDirectory`, `Load_ValidFile_ReturnsContext`, `Load_MissingFile_ReturnsNull`, `Load_EmptyFile_ReturnsNull`, `Load_MalformedJson_ReturnsNull` |
| Scenario 1 | `app/MyOwnPo.App.UnitTests` | `ProjectContextServiceTests` | `LoadFromFile_FileExists_LoadsContext`, `LoadFromFile_NoFile_RemainsEmpty`, `LoadFromFile_MalformedFile_RemainsEmpty`                                                                                              |
| Scenario 1 | `app/MyOwnPo.App.UnitTests` | `ConsoleHostTests`           | `Run_ContextFileExists_DisplaysLoadedMessage`, `Run_NoContextFile_NoLoadMessage`, `Run_MalformedContextFile_DisplaysWarning`                                                                                       |
| Scenario 2 | `app/MyOwnPo.App.UnitTests` | `ProjectContextServiceTests` | `SetContext_ValidContext_PersistsToFile`, `UpdateContext_ExistingContext_PersistsToFile`, `SetContext_SaveThrows_RetainsInMemoryContext`                                                                           |
| Scenario 2 | `app/MyOwnPo.App.UnitTests` | `JsonContextFileStoreTests`  | `Save_ContextWithNullFields_OmitsNullFieldsFromJson`, `Save_OverwritesExistingFile`                                                                                                                                |
| Scenario 3 | `app/MyOwnPo.App.UnitTests` | `JsonContextFileStoreTests`  | `Load_ExternallyEditedFile_LoadsCorrectValues`, `Load_MixedCasePropertyNames_LoadsCaseInsensitive`, `Load_PartialFields_ReturnsPartialContext`, `Load_ExtraProperties_IgnoresUnknownFields`                        |
| Scenario 4 | `app/MyOwnPo.App.UnitTests` | `ProjectContextServiceTests` | `ClearContext_WithFile_DeletesFile`, `ClearContext_NoFile_DoesNotThrow`                                                                                                                                            |
| Scenario 4 | `app/MyOwnPo.App.UnitTests` | `JsonContextFileStoreTests`  | `Delete_FileExists_RemovesFile`, `Delete_FileDoesNotExist_DoesNotThrow`                                                                                                                                            |

### Integration Tests

| Scenario | Test Project | Test Class | Method Pattern                                                                                                   |
| -------- | ------------ | ---------- | ---------------------------------------------------------------------------------------------------------------- |
| N/A      | —            | —          | No integration tests required — all persistence is local file I/O fully testable via unit tests with temp files. |

---

## Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj`
3. `dotnet format --verify-no-changes .\myownpo.slnx`
4. Manual smoke test: run the app, set context, exit, restart, verify context is loaded and confirmation is shown. Clear context, restart, verify no context is loaded and no file remains.

---

## Instruction Compliance Checklist _(mandatory)_

- [ ] `ConsoleHost` calls `IProjectContextService`, not `IContextFileStore` or file I/O directly.
- [ ] Business orchestration (persist-on-set, load-on-start, delete-on-clear) remains in `ProjectContextService`, not in `ConsoleHost`.
- [ ] DI wiring changes are defined in `Program.cs` (composition root).
- [ ] Security concerns addressed: no secrets involved in this feature; file path is local and user-controlled; no sensitive data beyond project context.
- [ ] Test additions align with project test expectations — unit tests for all new and modified classes.
- [ ] Style and naming adherence validated with `.editorconfig` and `dotnet format`.
- [ ] Malformed/missing files handled gracefully — no crashes, no silent data loss.

---

## Unresolved Blockers _(mandatory)_

~~**Context file location**~~: **Resolved** — The context file is `project-context.json` in the current working directory. The path is injectable via `IContextFileStore` for testability.

`None` — all blockers resolved.

---

## Revisions

### Session 2026-03-27

- **C1** (Consistency / HIGH): Constructor injection task for `IContextFileStore` lived only in scenario-2 but was needed first in scenario-1 → **Fix**: Moved TASK-2-01 into scenario-1 as TASK-1-02b; removed from scenario-2
- **C2** (Consistency / HIGH): Master plan Global Test Strategy missing 6 tests from scenario plans → **Fix**: Added `SetContext_SaveThrows_RetainsInMemoryContext`, `Save_ContextWithNullFields_OmitsNullFieldsFromJson`, `Save_OverwritesExistingFile`, `Load_ExternallyEditedFile_LoadsCorrectValues`, `Load_MixedCasePropertyNames_LoadsCaseInsensitive`, `ClearContext_NoFile_DoesNotThrow` to the Global Test Strategy table
- **A1** (Ambiguity / MEDIUM): TASK-1-03 offered two error-handling approaches without committing → **Fix**: Committed to `ContextLoadResult` enum approach; removed exception alternative
- **U1** (Underspec / MEDIUM): No task or file defined for `ContextLoadResult` enum → **Fix**: Added TASK-1-02c and `ContextLoadResult.cs` file entry in scenario-1
- **S1** (Spec-coverage / MEDIUM): Unknown JSON properties not preserved or warned about → **Fix**: Per user decision, unknown properties silently ignored and overwritten on re-save; on load, display loaded fields so user can confirm (TASK-1-04 updated)
- **D1** (Duplication / MEDIUM): Scenario-3 test names drifted from master plan canonical names → **Fix**: Renamed `Load_FileWithExtraProperties_IgnoresUnknown` → `Load_ExtraProperties_IgnoresUnknownFields` and `Load_FileWithSubsetOfFields_ReturnsPartialContext` → `Load_PartialFields_ReturnsPartialContext` in scenario-3
- **C3** (Consistency / LOW): Scenario-1 Technical Questions still referenced unresolved file path clarification → **Fix**: Updated to reflect resolved decision
