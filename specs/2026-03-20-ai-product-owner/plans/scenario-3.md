# Scenario Plan: Get Suggestions with Project Context

**Created**: 2026-03-20
**Spec**: [specs/2026-03-20-ai-product-owner/spec.md](specs/2026-03-20-ai-product-owner/spec.md)
**Master Plan**: [specs/2026-03-20-ai-product-owner/plans/plan.md](specs/2026-03-20-ai-product-owner/plans/plan.md)
**Scenario Priority**: P2
**Scenario Index**: 3

---

## Scenario Goal _(mandatory)_

Allow the team member to provide project context — product vision, business goals, target users, sprint focus, and constraints — so the AI Product Owner produces more relevant and informed prioritization suggestions. Context can be set before or after requesting suggestions, and can be updated at any time. Updated context replaces previous context in subsequent suggestions.

---

## Acceptance Traceability _(mandatory)_

- **Scenario Acceptance Criteria**:
  1. **Given** the team member provides a product vision and business goals, **When** they request prioritization suggestions, **Then** the justifications explicitly reference the provided vision and goals.
  2. **Given** previously provided context, **When** the team member updates it (e.g., changes the sprint focus), **Then** subsequent prioritization suggestions reflect the updated context.
  3. **Given** no context has been provided, **When** the team member requests prioritization suggestions, **Then** the AI Product Owner produces suggestions based on general best practices and notes that providing context would improve the results.
- **Related Requirements**: FR-008, FR-009

---

## Technical Context _(mandatory)_

- **Current Behavior**: After Scenario 2, the app has a conversational agent that can suggest priorities based on story content alone. No mechanism exists to accept or use project context.
- **Target Behavior**: A `context` command (or natural language like "set our vision to...") allows the team member to set/update project context fields. The `ProjectContext` is stored in memory and registered as an agent tool (`GetProjectContext`) so the agent can access it when generating suggestions. The agent's system prompt instructs it to reference context in justifications when available, and to note its absence when not.
- **Architecture Boundaries**:
  - `ConsoleHost` recognizes `context set`, `context show`, `context clear` as direct commands → calls `IProjectContextService`.
  - `ProjectContextService` manages the in-memory context state.
  - The agent retrieves context via its `GetProjectContext` tool (which calls `IProjectContextService.GetContext()`) and incorporates it into its reasoning.
  - No new data-access layer needed — context is in-memory only for MVP.
- **Dependencies**: `IProjectContextService` registered as a new agent tool. The agent's system prompt is updated to instruct context-aware reasoning.

---

## Implementation Tasks _(mandatory)_

- [ ] **TASK-3-01**: Define `ProjectContext` model with properties: `Vision` (string?), `BusinessGoals` (string?), `TargetUsers` (string?), `SprintFocus` (string?), `Constraints` (string?). See [models.md](specs/2026-03-20-ai-product-owner/plans/models.md#projectcontext) for full definition.
  - **Layer**: Models
  - **Reason**: Structured representation of the optional project context from the spec.

- [ ] **TASK-3-02**: Define `IProjectContextService` interface with methods: `void SetContext(ProjectContext context)`, `void UpdateContext(Action<ProjectContext> updater)`, `ProjectContext? GetContext()`, `void ClearContext()`, `bool HasContext`.
  - **Layer**: Services
  - **Reason**: Abstraction for managing context state. Allows `PrioritizationService` to check and retrieve context.

- [ ] **TASK-3-03**: Implement `ProjectContextService : IProjectContextService`.
  - `SetContext`: Store the context in memory (replace any previous context).
  - `UpdateContext`: Apply the updater action to the current context, or create a new one if none exists.
  - `GetContext`: Return current context or null.
  - `ClearContext`: Set stored context to null, resetting `HasContext` to false.
  - `HasContext`: Return whether context has been set.
  - Register as singleton in DI (context must persist across commands in the same session).
  - **Layer**: Services
  - **Reason**: Simple in-memory state management. No persistence needed for MVP.

- [ ] **TASK-3-04**: Register `GetProjectContext` as an agent tool in the Product Owner agent definition.
  - The tool calls `IProjectContextService.GetContext()` and returns the context as a structured result.
  - Update the agent's system prompt to instruct: "When generating prioritization suggestions, always check for project context using the GetProjectContext tool. If context exists, reference the vision, goals, and constraints in your justifications. If no context is set, note that providing context would improve the quality of suggestions."
  - **Layer**: Agents / Services
  - **Reason**: The agent needs access to context to produce context-aware suggestions. Registering as a tool follows the Agent Framework pattern and keeps the agent's reasoning flexible.

- [ ] **TASK-3-05**: Add `context` command to `ConsoleHost`.
  - `context set` — prompt the user for each field (vision, goals, target users, sprint focus, constraints) interactively, or accept them as `--vision "..." --goals "..."` flags.
  - `context show` — display the current context.
  - `context clear` — remove the current context.
  - **Layer**: Console Interaction
  - **Reason**: User-facing command for managing context.

- [ ] **TASK-3-06**: Register `IProjectContextService` as singleton in the DI container in `Program.cs`.
  - **Layer**: Composition Root
  - **Reason**: Must be singleton so context persists across commands within a session.

---

## Files To Alter _(mandatory)_

| File                                                    | Change Type | Why                                                                                            |
| ------------------------------------------------------- | ----------- | ---------------------------------------------------------------------------------------------- |
| `Models/ProjectContext.cs`                              | Add         | Project context model                                                                          |
| `Services/IProjectContextService.cs`                    | Add         | Context management interface                                                                   |
| `Services/ProjectContextService.cs`                     | Add         | In-memory context management                                                                   |
| `Agents/ProductOwnerAgentDefinition.cs`                 | Modify      | Add `GetProjectContext` tool registration and update system prompt for context-aware reasoning |
| `ConsoleHost.cs`                                        | Modify      | Add `context set`, `context show`, `context clear` commands                                    |
| `Program.cs`                                            | Modify      | Register `IProjectContextService` as singleton                                                 |
| `app/MyOwnPo.App.UnitTests/ProjectContextServiceTests.cs` | Add         | Unit tests for context management                                                              |
| `app/MyOwnPo.App.UnitTests/PrioritizationServiceTests.cs` | Modify      | Add tests verifying context flows into suggestions                                             |

---

## Technical Questions _(mandatory)_

- **Context input UX**: Should the `context set` command use an interactive multi-prompt (ask for each field one by one) or accept all fields as inline flags? Recommendation: support both — interactive mode when no flags are provided, flag-based mode for scripting/quick entry.
- **Context persistence across sessions**: Should context be saved to disk so it survives app restarts? Recommendation: not for MVP (in-memory only). Note this as a potential enhancement.
- **Partial context updates**: Should `context set` replace all fields or only the ones provided? Recommendation: `context set` replaces everything; add a `context update --sprint-focus "new focus"` variant for partial updates if needed.

---

## Testing Criteria _(mandatory)_

### Unit Tests

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `ProjectContextServiceTests`
- **Methods to add/update**:
  - `SetContext_ValidContext_StoresContext`
  - `SetContext_CalledTwice_ReplacesContext`
  - `GetContext_NoContextSet_ReturnsNull`
  - `HasContext_AfterSet_ReturnsTrue`
  - `HasContext_BeforeSet_ReturnsFalse`
  - `UpdateContext_ExistingContext_AppliesUpdate`
  - `ClearContext_AfterSet_RemovesContext` (if `ClearContext` method is added)

- **Class**: `PrioritizationServiceTests` (additions to existing)
- **Methods to add/update**:
  - `Suggest_WithContext_AgentReceivesContextViaTool`
  - `Suggest_WithContext_JustificationsReferenceContext`
  - `Suggest_WithoutContext_IncludesNoteAboutMissingContext`
  - `Suggest_AfterContextUpdate_AgentReceivesUpdatedContext`
  - `Resuggest_WithContext_AgentReceivesContextViaTool`

### Integration Tests

- **Type**: Integration
- **Required**: No — the context flow is fully testable at the unit level by mocking `IChatClient` and verifying the prompts sent to it. Integration tests from Scenario 2 already cover the end-to-end LLM flow.
- **Project**: N/A

---

## Scenario Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnxx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~ProjectContextService|FullyQualifiedName~PrioritizationService"`
3. `dotnet format --verify-no-changes .\myownpo.slnxx`
4. Manual smoke test: run the app, connect to a backlog.
   - Run `suggest` without context → verify output includes note about missing context.
   - Run `context set` → provide vision and goals.
   - Run `suggest` again → verify justifications reference the provided context.
   - Run `context set` with different values → run `suggest` → verify suggestions reflect updated context.

---

## Scenario Compliance Checks _(mandatory)_

- [ ] No direct context state manipulation from `ConsoleHost` — all go through `IProjectContextService`.
- [ ] Service boundary respected: `ProjectContextService` manages state; the agent accesses context via its registered `GetProjectContext` tool.
- [ ] Security: no sensitive context data logged at debug level (context may contain business-sensitive information).
- [ ] `IProjectContextService` registered as singleton to maintain state across commands.
- [ ] Agent tool `GetProjectContext` correctly wired and returning current state.
- [ ] Tests and naming conventions aligned with project standards.

---

## Revisions

### Session 2026-03-24

- **U1** (Underspec / CRITICAL): `IProjectContextService` was missing `ClearContext()` despite `context clear` command in TASK-3-05 and test in testing criteria. → **Fix**: Added `void ClearContext()` to TASK-3-02 interface and implementation behavior to TASK-3-03.
- **C2** (Consistency / MEDIUM): Files To Alter listed `PrioritizationService.cs` as Modify with "no changes needed" and `IPrioritizationService.cs` as No change. → **Fix**: Removed both rows.
- **C3** (Consistency / MEDIUM): Integration test reasoning referenced nonexistent `ILlmService`. → **Fix**: Replaced with `IChatClient`.

### Session 2026-03-24 #2

- **I1** (Instruction / CRITICAL): Scenario task layer labels used custom taxonomy. → **Fix**: Added explicit Layer Label Mapping that maps existing labels to template-required taxonomy.
