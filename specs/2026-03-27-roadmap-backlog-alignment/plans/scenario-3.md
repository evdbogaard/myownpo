# Scenario Plan: Recommend Project Context from the Roadmap

**Created**: 2026-03-27
**Spec**: [specs/2026-03-27-roadmap-backlog-alignment/spec.md](specs/2026-03-27-roadmap-backlog-alignment/spec.md)
**Master Plan**: [specs/2026-03-27-roadmap-backlog-alignment/plans/plan.md](specs/2026-03-27-roadmap-backlog-alignment/plans/plan.md)
**Models**: [specs/2026-03-27-roadmap-backlog-alignment/plans/models.md](specs/2026-03-27-roadmap-backlog-alignment/plans/models.md)
**Scenario Priority**: P2
**Scenario Index**: 3

---

## Scenario Goal _(mandatory)_

Generate a project context suggestion from roadmap themes that includes exactly one vision statement and exactly three goals, expressed in planning-friendly language and free of internal contradictions.

---

## Acceptance Traceability _(mandatory)_

- **Scenario Acceptance Criteria**:
  1. **Given** a roadmap with strategic themes, **When** context guidance is requested, **Then** the output includes exactly one vision statement and exactly 3 goals consistent with those themes.
  2. **Given** roadmap priorities with different time horizons, **When** project context is generated, **Then** goals are expressed in a way that supports planning decisions.
- **Related Requirements**: FR-009, FR-010, FR-013

---

## Technical Context _(mandatory)_

- **Current Behavior**: Context can be manually set and persisted through `ProjectContextService`, but there is no roadmap-driven context recommendation flow.
- **Target Behavior**: Chat responses from `PrioritizationService` include `ProjectContextSuggestion` content (one vision and three goals) derived from loaded roadmap themes and timelines.
- **Architecture Boundaries**:
  - `ConsoleHost` forwards user chat requests and renders returned text.
  - `PrioritizationService` performs synthesis and contradiction checks using in-memory loaded roadmap state.
  - Existing `IProjectContextService` remains separate and unchanged unless explicit save/apply command is added later.
- **Dependencies**: Scenario 1 roadmap parsing output and optional AI summarization support from existing chat client.
- **Model Reference**: Scenario 3 model contracts are defined in [models.md](specs/2026-03-27-roadmap-backlog-alignment/plans/models.md) (`ProjectContextSuggestion`, `RoadmapAnalysisResult`, `RoadmapItem`).

---

## Implementation Tasks _(mandatory)_

- [ ] **TASK-3-01**: Add context suggestion model (`ProjectContextSuggestion`) with one vision and three goals.
  - **Layer**: Common
  - **Reason**: Explicitly model FR-013 cardinality constraints using the shared model definition in [models.md](specs/2026-03-27-roadmap-backlog-alignment/plans/models.md).

- [ ] **TASK-3-02**: Extend `PrioritizationService` with context synthesis in chat tool-calling flow.
  - **Layer**: Service
  - **Reason**: Centralize roadmap-theme interpretation and planning-language generation.

- [ ] **TASK-3-03**: Add contradiction guard checks when themes conflict.
  - **Layer**: Service
  - **Reason**: Avoid incoherent outputs when roadmap includes competing initiatives.

- [ ] **TASK-3-04**: Extend console output to display vision and numbered goals in a dedicated section.
  - **Layer**: Controller
  - **Reason**: Keep roadmap analysis report readable and separated from linking/gap sections.

- [ ] **TASK-3-05**: Support context guidance from natural-language prompts (for example, "based on current roadmap, suggest vision and goals") using the loaded roadmap in memory.
  - **Layer**: Service
  - **Reason**: User requested chat-driven flow without dedicated roadmap command.

---

## Files To Alter _(mandatory)_

| File                                                      | Change Type | Why                                                                |
| --------------------------------------------------------- | ----------- | ------------------------------------------------------------------ |
| `app/MyOwnPo.App/Models/ProjectContextSuggestion.cs`      | Add         | Model one-vision/three-goals output                                |
| `app/MyOwnPo.App/Models/RoadmapAnalysisResult.cs`         | Modify      | Include optional context suggestion block                          |
| `app/MyOwnPo.App/Services/IPrioritizationService.cs`      | Modify      | Ensure contract supports roadmap context guidance in chat          |
| `app/MyOwnPo.App/Services/PrioritizationService.cs`       | Modify      | Implement vision/goal synthesis and conflict handling in tool flow |
| `app/MyOwnPo.App/ConsoleHost.cs`                          | Modify      | Keep chat-driven context requests and output rendering             |
| `app/MyOwnPo.App.UnitTests/PrioritizationServiceTests.cs` | Modify      | Context synthesis behavior tests                                   |
| `app/MyOwnPo.App.UnitTests/ConsoleHostTests.cs`           | Modify      | Chat routing tests for context request                             |

---

## Technical Questions _(mandatory)_

- Context generation runs only when the user explicitly asks in normal chat (no dedicated command). It may include comparison between roadmap-derived context and currently set project context when requested.
- Context guidance results are text-only and must never update stored project context directly.
- Return only the goals that can be formed; if fewer than three are available (for example 2), return those and include a warning that there was insufficient content for the missing goal(s).

---

## Testing Criteria _(mandatory)_

### Unit Tests

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `PrioritizationServiceTests`
- **Methods to add/update**:
  - `Chat_ContextFromRoadmapWithThemes_ReturnsOneVisionAndThreeGoals`
  - `Chat_ContextFromConflictingThemes_ReturnsNonContradictoryGoals`
  - `Chat_ContextFromSparseRoadmapContent_ReturnsFallbackPlanningGoals`

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `ConsoleHostTests`
- **Methods to add/update**:
  - `HandleInput_ContextFromRoadmapRequest_ForwardsToPrioritizationService`

### Integration Tests

- **Type**: Integration
- **Required**: No, this scenario is verified through unit tests and runtime smoke checks.
- **Project**: N/A
- **Class**: N/A
- **Methods to add/update**:
  - None

---

## Scenario Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~PrioritizationServiceTests.Chat_ContextFromRoadmap|FullyQualifiedName~ConsoleHostTests.HandleInput_ContextFromRoadmapRequest"`
3. `dotnet format --verify-no-changes .\myownpo.slnx`
4. Run app smoke test with roadmap loaded through chat and verify one vision plus exactly three goals are returned on follow-up context requests.

---

## Scenario Compliance Checks _(mandatory)_

- [ ] Context recommendation orchestration remains in `PrioritizationService` tool-calling flow.
- [ ] Console layer is responsible only for chat routing and rendering.
- [ ] Cardinality constraints (one vision, three goals) are asserted in tests.
- [ ] Style and naming standards are validated with repository tooling.
