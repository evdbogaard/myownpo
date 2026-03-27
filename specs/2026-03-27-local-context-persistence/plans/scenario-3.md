# Scenario Plan: Edit Context File Externally

**Created**: 2026-03-27
**Spec**: [specs/2026-03-27-local-context-persistence/spec.md](specs/2026-03-27-local-context-persistence/spec.md)
**Master Plan**: [specs/2026-03-27-local-context-persistence/plans/plan.md](specs/2026-03-27-local-context-persistence/plans/plan.md)
**Scenario Priority**: P2
**Scenario Index**: 3

---

## Scenario Goal _(mandatory)_

Users can create or edit the project context JSON file in any text editor. Their changes are picked up the next time the application starts, with the same behavior as if context had been set through the application.

---

## Acceptance Traceability _(mandatory)_

- **Scenario Acceptance Criteria**:
  1. **Given** a user has manually edited the context JSON file in an external editor, **When** the application starts, **Then** the edited values are loaded as the active project context.
  2. **Given** a user creates a new context JSON file from scratch following the expected format, **When** the application starts, **Then** the context is loaded successfully.
- **Related Requirements**: FR-002, FR-003, FR-008, FR-010
- **Related Edge Cases**: Partial fields, extra/unrecognized properties, case sensitivity in property names

---

## Technical Context _(mandatory)_

- **Current Behavior**: No context file support exists. After Scenarios 1 and 2 are implemented, the application loads context from a JSON file on startup and saves context to that file on set.
- **Target Behavior**: The JSON deserialization is lenient enough to handle externally authored files: case-insensitive property names, unknown properties ignored, partial fields loaded as-is. The JSON format uses clearly named properties matching `ProjectContext` fields: `Vision`, `BusinessGoals`, `TargetUsers`, `SprintFocus`, `Constraints` (FR-010).
- **Architecture Boundaries**: No changes to architecture — this scenario validates that the `JsonContextFileStore.Load()` implementation from Scenario 1 handles externally edited files correctly.
- **Dependencies**: `JsonContextFileStore` from Scenario 1 with `PropertyNameCaseInsensitive = true` and default handling for unknown properties.

---

## Implementation Tasks _(mandatory)_

- [ ] **TASK-3-01**: Verify `JsonContextFileStore.Load()` uses `PropertyNameCaseInsensitive = true` in `JsonSerializerOptions` so externally edited files with varying casing are handled.
  - **Layer**: Service (infrastructure)
  - **Reason**: External editors may not match exact casing. Case-insensitive deserialization ensures resilience.

- [ ] **TASK-3-02**: Verify `System.Text.Json` default behavior ignores unknown properties during deserialization (it does by default — no `JsonUnmappedMemberHandling` override needed).
  - **Layer**: Service (infrastructure)
  - **Reason**: Edge case from spec — unrecognized sections/properties are ignored and do not prevent loading.

- [ ] **TASK-3-03**: Add unit tests that exercise externally edited file scenarios: hand-crafted JSON with mixed casing, extra properties, subset of fields, and a from-scratch file with all fields.
  - **Layer**: Tests
  - **Reason**: Validates AC 1 and AC 2 with realistic external edit scenarios.

---

## Files To Alter _(mandatory)_

| File                                                     | Change Type                 | Why                                                                         |
| -------------------------------------------------------- | --------------------------- | --------------------------------------------------------------------------- |
| `app/MyOwnPo.App/Services/JsonContextFileStore.cs`       | Verify (possibly no change) | Confirm `PropertyNameCaseInsensitive` and default unknown-property handling |
| `app/MyOwnPo.App.UnitTests/JsonContextFileStoreTests.cs` | Modify                      | Add external-edit scenario tests                                            |

---

## Technical Questions _(mandatory)_

- **Should unrecognized properties be preserved on re-save?** **Decision**: No. Unknown properties are silently ignored on load and overwritten on re-save. The loaded `ProjectContext` displays its fields on startup (Scenario 1, TASK-1-04) so the user can confirm what was loaded. No warning about dropped properties is needed — the user sees exactly what the application is working with.

---

## Testing Criteria _(mandatory)_

### Unit Tests

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `JsonContextFileStoreTests`
- **Methods to add**:
  - `Load_ExternallyEditedFile_LoadsCorrectValues` — JSON with all fields, hand-written (not produced by the app)
  - `Load_MixedCasePropertyNames_LoadsCaseInsensitive` — JSON with `"vision"`, `"BUSINESSGOALS"`, etc.
  - `Load_ExtraProperties_IgnoresUnknownFields` — JSON with `"Vision": "...", "CustomField": "ignored"` (matches master plan canonical name)
  - `Load_PartialFields_ReturnsPartialContext` — JSON with only `"Vision"` and `"SprintFocus"` (matches master plan canonical name)

### Integration Tests

- **Type**: Integration
- **Required**: No — all scenarios testable with in-memory JSON content and temp files.

---

## Scenario Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~JsonContextFileStoreTests.Load"`
3. `dotnet format --verify-no-changes .\myownpo.slnx`
4. Manual smoke: create a `project-context.json` by hand with a text editor, start the app, use `context show` to verify all fields are loaded correctly.

---

## Scenario Compliance Checks _(mandatory)_

- [ ] No direct file I/O from `ConsoleHost` — all loading through `IProjectContextService` → `IContextFileStore`.
- [ ] Service boundary respected — deserialization logic stays in `JsonContextFileStore`.
- [ ] Security checks and sensitive logging review complete — no secrets; user-authored JSON is treated as untrusted input (malformed JSON handled gracefully, no code execution from JSON).
- [ ] Tests and naming conventions aligned with project standards (xUnit, Moq, `[Method]_[Condition]_[Result]` pattern).

---

## Revisions

### Session 2026-03-27

- **S1** (Spec-coverage / MEDIUM): Unknown properties policy updated → **Fix**: Silently ignore unknown properties; no warning; overwrite on re-save. User confirms loaded context via startup display (Scenario 1 TASK-1-04)
- **D1** (Duplication / MEDIUM): Test names drifted from master plan → **Fix**: Renamed `Load_FileWithExtraProperties_IgnoresUnknown` → `Load_ExtraProperties_IgnoresUnknownFields` and `Load_FileWithSubsetOfFields_ReturnsPartialContext` → `Load_PartialFields_ReturnsPartialContext`
