# Scenario Plan: Suggest Missing Stories from Roadmap Gaps

**Created**: 2026-03-27
**Spec**: [specs/2026-03-27-roadmap-backlog-alignment/spec.md](specs/2026-03-27-roadmap-backlog-alignment/spec.md)
**Master Plan**: [specs/2026-03-27-roadmap-backlog-alignment/plans/plan.md](specs/2026-03-27-roadmap-backlog-alignment/plans/plan.md)
**Models**: [specs/2026-03-27-roadmap-backlog-alignment/plans/models.md](specs/2026-03-27-roadmap-backlog-alignment/plans/models.md)
**Scenario Priority**: P1
**Scenario Index**: 2

---

## Scenario Goal _(mandatory)_

For every roadmap item not sufficiently represented by existing New-state backlog stories, generate attributable, unique, refinement-ready story title suggestions (1-5 per item) using Microsoft Learn MCP guidance in this scenario.

---

## Acceptance Traceability _(mandatory)_

- **Scenario Acceptance Criteria**:
  1. **Given** roadmap items with no sufficient backlog coverage, **When** the analysis is run, **Then** the system proposes new story titles for those gaps.
  2. **Given** multiple uncovered roadmap items, **When** suggestions are produced, **Then** each item receives clearly attributable suggested titles.
  3. **Given** an uncovered roadmap item, **When** title suggestions are generated, **Then** between 1 and 5 suggested titles are provided.
- **Related Requirements**: FR-004, FR-005, FR-006, FR-007, FR-010, FR-012

---

## Technical Context _(mandatory)_

- **Current Behavior**: No service currently produces backlog gap suggestions from roadmap content. Microsoft Learn MCP is not yet integrated into runtime flow.
- **Target Behavior**: `PrioritizationService` detects uncovered roadmap items from loaded in-memory roadmap state and calls `IMicrosoftLearnMcpGateway` to gather guidance snippets. `MicrosoftLearnMcpGateway` uses the `ModelContextProtocol` package with `IMcpToolProvider` to invoke Microsoft Learn MCP tools. The service then generates 1-5 title suggestions per uncovered item and returns those grouped by roadmap item through chat responses.
- **Architecture Boundaries**:
  - `ConsoleHost` displays suggestions only.
  - `PrioritizationService` orchestrates uncovered detection and title suggestion generation through tool functions.
  - `IMicrosoftLearnMcpGateway` performs only external Microsoft Learn MCP operations.
- **Dependencies**: Scenario 1 models/results, `ModelContextProtocol` (`IMcpToolProvider`), Microsoft Learn MCP gateway/settings, and deterministic title-quality guardrails.
- **Model Reference**: Scenario 2 model contracts are defined in [models.md](specs/2026-03-27-roadmap-backlog-alignment/plans/models.md) (`RoadmapGapSuggestion`, `RoadmapAnalysisResult`, `RoadmapItem`).

---

## Implementation Tasks _(mandatory)_

- [ ] **TASK-2-01**: Extend roadmap analysis model with explicit gap suggestion entities.
  - **Layer**: Common
  - **Reason**: Preserve traceability from uncovered roadmap item to suggested story titles by implementing model definitions in [models.md](specs/2026-03-27-roadmap-backlog-alignment/plans/models.md).

- [ ] **TASK-2-02**: Add `IMicrosoftLearnMcpGateway` and `MicrosoftLearnMcpGateway` for guidance retrieval scoped to Scenario 2.
  - **Layer**: Repository
  - **Reason**: Satisfy FR-007 while isolating external integration concerns.

- [ ] **TASK-2-02b**: Add `ModelContextProtocol` package reference and implement Microsoft Learn MCP tool invocation via `IMcpToolProvider` in `MicrosoftLearnMcpGateway`.
  - **Layer**: Repository
  - **Reason**: Lock implementation to the approved MCP integration contract.

- [ ] **TASK-2-03**: Add `GenerateGapSuggestions` flow in `PrioritizationService` tool-calling logic.
  - **Layer**: Service
  - **Reason**: Convert uncovered roadmap items and guidance snippets into 1-5 unique titles per item.

- [ ] **TASK-2-04**: Add title quality heuristics and deduplication checks.
  - **Layer**: Service
  - **Reason**: Enforce understandable, attributable, non-duplicate suggestions.

- [ ] **TASK-2-05**: Ensure chat responses render sectioned gap suggestions per roadmap item.
  - **Layer**: Controller
  - **Reason**: Ensure review output remains clear and attributable.

- [ ] **TASK-2-06**: Register Microsoft Learn MCP gateway/settings in `Program.cs`.
  - **Layer**: Common
  - **Reason**: Enable runtime injection and configuration validation.

---

## Files To Alter _(mandatory)_

| File                                                         | Change Type | Why                                                                      |
| ------------------------------------------------------------ | ----------- | ------------------------------------------------------------------------ |
| `app/MyOwnPo.App/Models/RoadmapGapSuggestion.cs`             | Add         | Represent per-item title suggestions                                     |
| `app/MyOwnPo.App/Models/RoadmapAnalysisResult.cs`            | Modify      | Include gap suggestion results                                           |
| `app/MyOwnPo.App/Gateways/IMicrosoftLearnMcpGateway.cs`      | Add         | Gateway contract for Microsoft Learn MCP guidance                        |
| `app/MyOwnPo.App/Gateways/MicrosoftLearnMcpGateway.cs`       | Add         | Runtime integration for Microsoft Learn MCP search/guidance              |
| `app/MyOwnPo.App/Gateways/MicrosoftLearnMcpSettings.cs`      | Add         | Configuration model for MCP endpoint/auth options                        |
| `app/MyOwnPo.App/MyOwnPo.App.csproj`                         | Modify      | Add `ModelContextProtocol` package reference                             |
| `app/MyOwnPo.App/Services/PrioritizationService.cs`          | Modify      | Add uncovered-item guidance and title generation logic in chat/tool flow |
| `app/MyOwnPo.App/ConsoleHost.cs`                             | Modify      | Preserve chat-first rendering for roadmap gap suggestions                |
| `app/MyOwnPo.App/Program.cs`                                 | Modify      | DI and settings binding for MCP gateway                                  |
| `app/MyOwnPo.App.UnitTests/PrioritizationServiceTests.cs`    | Modify      | Gap suggestion behavior and constraints tests                            |
| `app/MyOwnPo.App.UnitTests/MicrosoftLearnMcpGatewayTests.cs` | Add         | Gateway request/response and failure handling tests                      |

---

## Technical Questions _(mandatory)_

- Guidance retrieval failures must return an error for that specific uncovered roadmap item; do not generate partial/fallback title suggestions for that item.

---

## Testing Criteria _(mandatory)_

### Unit Tests

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `PrioritizationServiceTests`
- **Methods to add/update**:
  - `Chat_UncoveredRoadmapItem_GeneratesOneToFiveTitles`
  - `Chat_MultipleUncoveredRoadmapItems_GeneratesAttributableSuggestionsPerItem`
  - `Chat_DuplicateTitleCandidate_RemovesDuplicateSuggestion`
  - `Chat_GuidanceUnavailableForItem_ReturnsItemLevelErrorWithoutFallbackTitles`

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `MicrosoftLearnMcpGatewayTests`
- **Methods to add/update**:
  - `SearchGuidance_ValidRoadmapItem_ReturnsGuidanceSnippets`
  - `SearchGuidance_McpError_ThrowsDescriptiveException`

### Integration Tests

- **Type**: Integration
- **Required**: No, this plan defers integration coverage and validates behavior through unit tests plus runtime smoke checks.
- **Project**: N/A
- **Class**: N/A
- **Methods to add/update**:
  - None

---

## Scenario Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~PrioritizationServiceTests.Chat_UncoveredRoadmapItem|FullyQualifiedName~MicrosoftLearnMcpGatewayTests"`
3. `dotnet format --verify-no-changes .\myownpo.slnx`
4. Run app smoke test with roadmap gaps and verify each uncovered item prints 1-5 suggestions and guidance attribution.

---

## Scenario Compliance Checks _(mandatory)_

- [ ] `ConsoleHost` remains service-driven and does not call MCP gateway directly.
- [ ] MCP integration remains isolated inside a gateway abstraction.
- [ ] Suggestion business rules (1-5, unique, attributable) are enforced in `PrioritizationService` tool-calling flow.
- [ ] Tests include explicit FR-007 coverage for Microsoft Learn MCP usage.
