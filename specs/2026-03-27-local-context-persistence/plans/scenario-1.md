# Scenario Plan: Resume Work With Previously Saved Context

**Created**: 2026-03-27
**Spec**: [specs/2026-03-27-local-context-persistence/spec.md](specs/2026-03-27-local-context-persistence/spec.md)
**Master Plan**: [specs/2026-03-27-local-context-persistence/plans/plan.md](specs/2026-03-27-local-context-persistence/plans/plan.md)
**Scenario Priority**: P1
**Scenario Index**: 1

---

## Scenario Goal _(mandatory)_

When the application starts, it automatically loads previously saved project context from a local JSON file so the user can resume working immediately without re-entering context. The user sees a confirmation message when context is restored, a warning if the file is malformed, and no message if no file exists.

---

## Acceptance Traceability _(mandatory)_

- **Scenario Acceptance Criteria**:
  1. **Given** a context file exists locally with all fields populated, **When** the application starts, **Then** the project context is automatically loaded and available without any user action.
  2. **Given** a context file exists locally, **When** the application starts and the context is loaded, **Then** the user sees a confirmation message indicating context was restored.
  3. **Given** no context file exists locally, **When** the application starts, **Then** the application starts normally with no context loaded and no error is shown.
- **Related Requirements**: FR-002, FR-004, FR-006, FR-007, FR-008
- **Related Edge Cases**: No context file on first run, malformed file, empty file, partial fields

---

## Technical Context _(mandatory)_

- **Current Behavior**: `ProjectContextService` starts with `_context = null`. There is no file loading. `ConsoleHost.Run` immediately enters the command loop without any startup initialization.
- **Target Behavior**: On startup, `ConsoleHost.Run` calls a load method on `IProjectContextService` that delegates to `IContextFileStore.Load()`. If context is returned, it is set in memory and a confirmation message is written. If the file is missing or empty, startup proceeds silently. If the file is malformed, a warning is displayed and startup proceeds without context.
- **Architecture Boundaries**: `ConsoleHost` calls `IProjectContextService` only. `ProjectContextService` calls `IContextFileStore`. File I/O lives entirely in `JsonContextFileStore`.
- **Dependencies**: `IContextFileStore` (new — cross-scenario task), `System.Text.Json` (framework-provided).

---

## Implementation Tasks _(mandatory)_

- [ ] **TASK-1-01**: Create `IContextFileStore` interface in `Services/` with methods: `ProjectContext? Load()`, `void Save(ProjectContext context)`, `void Delete()`.
  - **Layer**: Service (abstraction)
  - **Reason**: Testability boundary for file I/O. All scenarios depend on this interface.

- [ ] **TASK-1-02**: Implement `JsonContextFileStore : IContextFileStore` in `Services/`.
  - `Load()`: read file at configured path. If file does not exist, return `null`. If file is empty, return `null`. If JSON is malformed, throw a descriptive exception (let the caller decide how to handle). If JSON is valid, deserialize to `ProjectContext` using `System.Text.Json` with `JsonSerializerOptions { PropertyNameCaseInsensitive = true }`. Unrecognized properties are ignored by default.
  - `Save()`: serialize `ProjectContext` to JSON with `JsonSerializerOptions { WriteIndented = true }` and write to the configured path. Create the directory if it does not exist.
  - `Delete()`: delete the file if it exists; no-op if it does not.
  - Constructor accepts the file path as a string parameter.
  - **Layer**: Service (infrastructure implementation)
  - **Reason**: Concrete file I/O implementation. Kept simple and focused on data access.

- [ ] **TASK-1-02b**: Modify `ProjectContextService` constructor to accept `IContextFileStore` via dependency injection.
  - **Layer**: Service
  - **Reason**: The service needs the file store for load, save, and delete operations. This dependency is first needed here (for `LoadFromFile`) and reused by Scenarios 2 and 4.

- [ ] **TASK-1-02c**: Create `ContextLoadResult` enum in `Services/` with values: `Loaded`, `NoFile`, `Malformed`.
  - **Layer**: Service
  - **Reason**: Return type for `LoadFromFile` — avoids exceptions for expected cases (missing file is not exceptional).

- [ ] **TASK-1-03**: Add `LoadFromFile()` method to `IProjectContextService` and `ProjectContextService`.
  - Returns `ContextLoadResult`. Call `IContextFileStore.Load()`. If a non-null, non-empty context is returned, store it via the in-memory field and return `Loaded`. If `Load()` returns null, return `NoFile`. If `Load()` throws (malformed file), catch the exception and return `Malformed`.
  - **Layer**: Service
  - **Reason**: Orchestrates the load-from-file logic. Keeps error-handling policy in the service, not in ConsoleHost.

- [ ] **TASK-1-04**: Update `ConsoleHost.Run` to call `LoadFromFile()` before the command loop.
  - Switch on the returned `ContextLoadResult`:
    - `Loaded`: display the loaded context fields (vision, goals, etc.) so the user can confirm, e.g., `"Project context loaded from file:"` followed by each populated field.
    - `NoFile`: write nothing.
    - `Malformed`: write `"Warning: Could not read project context file. Starting without context."`.
  - **Layer**: Console Interaction (controller-equivalent)
  - **Reason**: User-facing notification. Must not contain file I/O or business logic.

- [ ] **TASK-1-05**: Register `IContextFileStore` / `JsonContextFileStore` in `Program.cs` with the file path defaulting to `project-context.json`.
  - **Layer**: Composition Root
  - **Reason**: DI wiring for the new dependency.

---

## Files To Alter _(mandatory)_

| File                                                      | Change Type | Why                                                               |
| --------------------------------------------------------- | ----------- | ----------------------------------------------------------------- |
| `app/MyOwnPo.App/Services/IContextFileStore.cs`           | Add         | New interface for file persistence abstraction                    |
| `app/MyOwnPo.App/Services/JsonContextFileStore.cs`        | Add         | Concrete JSON file store implementation                           |
| `app/MyOwnPo.App/Services/ContextLoadResult.cs`           | Add         | New enum for `LoadFromFile` return type                           |
| `app/MyOwnPo.App/Services/IProjectContextService.cs`      | Modify      | Add `LoadFromFile()` method returning `ContextLoadResult`         |
| `app/MyOwnPo.App/Services/ProjectContextService.cs`       | Modify      | Accept `IContextFileStore` dependency; implement `LoadFromFile()` |
| `app/MyOwnPo.App/ConsoleHost.cs`                          | Modify      | Call `LoadFromFile()` on startup; display messages                |
| `app/MyOwnPo.App/Program.cs`                              | Modify      | Register `IContextFileStore` / `JsonContextFileStore`             |
| `app/MyOwnPo.App.UnitTests/JsonContextFileStoreTests.cs`  | Add         | Tests for file store                                              |
| `app/MyOwnPo.App.UnitTests/ProjectContextServiceTests.cs` | Modify      | Tests for `LoadFromFile`                                          |
| `app/MyOwnPo.App.UnitTests/ConsoleHostTests.cs`           | Modify      | Tests for startup load messages                                   |

---

## Technical Questions _(mandatory)_

- ~~**File path default**~~: **Resolved** — `project-context.json` in the current working directory. Confirmed in master plan blocker resolution.
- ~~**Error handling strategy for `LoadFromFile`**~~: **Resolved** — `LoadFromFile` returns a `ContextLoadResult` enum (`Loaded`, `NoFile`, `Malformed`). No exceptions for expected cases. `ConsoleHost` uses a switch expression on the result.

---

## Testing Criteria _(mandatory)_

### Unit Tests

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `JsonContextFileStoreTests`
- **Methods to add**:
  - `Load_ValidFile_ReturnsContext`
  - `Load_MissingFile_ReturnsNull`
  - `Load_EmptyFile_ReturnsNull`
  - `Load_MalformedJson_ReturnsNull`
  - `Load_PartialFields_ReturnsPartialContext`
  - `Load_ExtraProperties_IgnoresUnknownFields`
  - `Save_ValidContext_WritesJsonFile`
  - `Delete_FileExists_RemovesFile`
  - `Delete_FileDoesNotExist_DoesNotThrow`

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `ProjectContextServiceTests`
- **Methods to add**:
  - `LoadFromFile_FileExists_LoadsContext`
  - `LoadFromFile_NoFile_RemainsEmpty`
  - `LoadFromFile_MalformedFile_RemainsEmpty`

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `ConsoleHostTests`
- **Methods to add**:
  - `Run_ContextFileExists_DisplaysLoadedMessage`
  - `Run_NoContextFile_NoLoadMessage`
  - `Run_MalformedContextFile_DisplaysWarning`

### Integration Tests

- **Type**: Integration
- **Required**: No — all file I/O is local and fully testable with temp files in unit tests.

---

## Scenario Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~JsonContextFileStoreTests|FullyQualifiedName~ProjectContextServiceTests.LoadFromFile|FullyQualifiedName~ConsoleHostTests.Run_Context"`
3. `dotnet format --verify-no-changes .\myownpo.slnx`
4. Manual smoke: create a `project-context.json` with sample values, start the app, verify context is loaded and message is displayed. Delete the file, restart, verify clean startup.

---

## Scenario Compliance Checks _(mandatory)_

- [ ] No direct file I/O from `ConsoleHost` — all persistence through `IProjectContextService` → `IContextFileStore`.
- [ ] Service boundary respected — `ProjectContextService` orchestrates load logic; `JsonContextFileStore` handles file I/O only.
- [ ] Security checks and sensitive logging review complete — no secrets involved; file path is user-controlled local path.
- [ ] Tests and naming conventions aligned with project standards (xUnit, Moq, `[Method]_[Condition]_[Result]` pattern).

---

## Revisions

### Session 2026-03-27

- **C1** (Consistency / HIGH): Constructor injection of `IContextFileStore` was only in scenario-2 but needed here first → **Fix**: Added TASK-1-02b for constructor injection in this scenario
- **A1** (Ambiguity / MEDIUM): TASK-1-03 had two error-handling approaches → **Fix**: Committed to `ContextLoadResult` enum; removed exception alternative
- **U1** (Underspec / MEDIUM): No task for `ContextLoadResult` enum → **Fix**: Added TASK-1-02c and `ContextLoadResult.cs` in Files To Alter
- **S1** (Spec-coverage / MEDIUM): On load, display loaded context fields so user can confirm → **Fix**: Updated TASK-1-04 to show loaded fields on startup
- **C3** (Consistency / LOW): Technical Questions referenced unresolved file path → **Fix**: Marked both questions as resolved
