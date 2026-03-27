# Scenario Plan: Link Roadmap Items to Existing Backlog Stories

**Created**: 2026-03-27
**Spec**: [specs/2026-03-27-roadmap-backlog-alignment/spec.md](specs/2026-03-27-roadmap-backlog-alignment/spec.md)
**Master Plan**: [specs/2026-03-27-roadmap-backlog-alignment/plans/plan.md](specs/2026-03-27-roadmap-backlog-alignment/plans/plan.md)
**Models**: [specs/2026-03-27-roadmap-backlog-alignment/plans/models.md](specs/2026-03-27-roadmap-backlog-alignment/plans/models.md)
**Scenario Priority**: P1
**Scenario Index**: 1

---

## Scenario Goal _(mandatory)_

Enable a product owner to ask in chat for roadmap analysis and receive deterministic link recommendations between roadmap items and existing backlog stories in New state, with plain-language rationale and confidence cues while enforcing one-story-to-one-roadmap-item cardinality.

---

## Acceptance Traceability _(mandatory)_

- **Scenario Acceptance Criteria**:
  1. **Given** a roadmap file and a backlog with related stories, **When** the analysis is run, **Then** the output identifies which stories align to each roadmap item.
  2. **Given** an identified story-to-roadmap link, **When** the user reviews the result, **Then** a concise reason for that link is provided in plain business language.
  3. **Given** a roadmap item that has no related story, **When** the analysis is completed, **Then** the roadmap item is listed as not currently represented in the backlog.
  4. **Given** a backlog story already linked to a roadmap item, **When** a second roadmap item is evaluated, **Then** that same story is not linked to the second roadmap item.
- **Related Requirements**: FR-001, FR-002, FR-003, FR-004, FR-008, FR-010, FR-011

---

## Technical Context _(mandatory)_

- **Current Behavior**: The app can load backlog stories and perform prioritization chat but has no roadmap ingestion or roadmap-to-story matching capability.
- **Target Behavior**: A natural-language chat request triggers tool function-calling in `PrioritizationService` to load and parse roadmap markdown from a hardcoded file path on disk, evaluate only New-state stories, and return grouped output with linked and unlinked roadmap items. Loaded roadmap data is retained in memory for subsequent chat turns.
- **Architecture Boundaries**:
  - `ConsoleHost` routes chat input to `IPrioritizationService`.
  - `PrioritizationService` performs matching orchestration through tool functions and returns response text.
  - `IBacklogService` remains the source of currently loaded stories.
  - Loader/parser abstractions handle file access and markdown extraction.
- **Dependencies**: New parser and service abstractions, existing `IBacklogService`, optional AI assistance via existing `IChatClient` when computing rationales.
- **Model Reference**: Scenario 1 model contracts are defined in [models.md](specs/2026-03-27-roadmap-backlog-alignment/plans/models.md) (`RoadmapItem`, `RoadmapStoryLinkRecommendation`, `RoadmapAnalysisResult`).

---

## Implementation Tasks _(mandatory)_

- [ ] **TASK-1-01**: Add roadmap analysis domain models.
  - **Layer**: Common
  - **Reason**: Establish explicit, testable contracts for link recommendations and unlinked roadmap items; implement the models defined in [models.md](specs/2026-03-27-roadmap-backlog-alignment/plans/models.md).

- [ ] **TASK-1-02**: Implement roadmap file loading and markdown parsing (`IRoadmapFileLoader`, `RoadmapMarkdownFileLoader`, `IRoadmapParser`, `RoadmapMarkdownParser`).
  - **Layer**: Service
  - **Reason**: FR-001 requires roadmap files from disk as primary input.

- [ ] **TASK-1-03**: Extend `PrioritizationService` with tool functions for roadmap load and story-link evaluation.
  - **Layer**: Service
  - **Reason**: Encapsulate matching logic, rationale generation, confidence estimation, and one-story-to-one-roadmap-item enforcement in chat-driven flow.

- [ ] **TASK-1-04**: Add in-memory loaded roadmap state in `PrioritizationService` so follow-up chat decisions reuse the same roadmap without reloading.
  - **Layer**: Service
  - **Reason**: Supports conversational continuity across multiple user prompts.

- [ ] **TASK-1-05**: Keep `ConsoleHost` chat-first and ensure roadmap requests are routed through `IPrioritizationService.Chat`.
  - **Layer**: Controller
  - **Reason**: User requested no dedicated roadmap command and conversational tool-calling behavior.

- [ ] **TASK-1-06**: Add DI registration for roadmap loader/parser and updated `PrioritizationService` dependencies in `Program.cs`.
  - **Layer**: Common
  - **Reason**: Maintain composition-root ownership for dependencies.

---

## Files To Alter _(mandatory)_

| File                                                       | Change Type | Why                                                                       |
| ---------------------------------------------------------- | ----------- | ------------------------------------------------------------------------- |
| `app/MyOwnPo.App/Models/RoadmapItem.cs`                    | Add         | Represent parsed roadmap initiatives                                      |
| `app/MyOwnPo.App/Models/RoadmapStoryLinkRecommendation.cs` | Add         | Represent story-to-roadmap link + rationale + confidence                  |
| `app/MyOwnPo.App/Models/RoadmapAnalysisResult.cs`          | Add         | Aggregate linked and unlinked analysis output                             |
| `app/MyOwnPo.App/Services/IRoadmapFileLoader.cs`           | Add         | File-loading abstraction for roadmap markdown                             |
| `app/MyOwnPo.App/Services/RoadmapMarkdownFileLoader.cs`    | Add         | Disk-backed roadmap file loading                                          |
| `app/MyOwnPo.App/Services/IRoadmapParser.cs`               | Add         | Parsing abstraction for roadmap markdown                                  |
| `app/MyOwnPo.App/Services/RoadmapMarkdownParser.cs`        | Add         | Markdown-to-roadmap-item extraction                                       |
| `app/MyOwnPo.App/Services/IPrioritizationService.cs`       | Modify      | Add roadmap-analysis chat contract requirements                           |
| `app/MyOwnPo.App/Services/PrioritizationService.cs`        | Modify      | Add roadmap tool functions, linking logic, and in-memory roadmap state    |
| `app/MyOwnPo.App/ConsoleHost.cs`                           | Modify      | Keep chat-driven flow and forward roadmap requests to prioritization chat |
| `app/MyOwnPo.App/Program.cs`                               | Modify      | Register new roadmap services                                             |
| `app/MyOwnPo.App.UnitTests/RoadmapMarkdownParserTests.cs`  | Add         | Parser behavior tests                                                     |
| `app/MyOwnPo.App.UnitTests/PrioritizationServiceTests.cs`  | Modify      | Roadmap story-link chat/tool tests                                        |
| `app/MyOwnPo.App.UnitTests/ConsoleHostTests.cs`            | Modify      | Chat routing tests for roadmap requests                                   |

---

## Technical Questions _(mandatory)_

- Roadmap source is fixed: the roadmap loader tool reads from a hardcoded filename/path on disk.
- Confidence is numeric percentage in the range 0% to 100%.

---

## Testing Criteria _(mandatory)_

### Unit Tests

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `RoadmapMarkdownParserTests`
- **Methods to add/update**:
  - `Parse_RoadmapWithBulletsAndHeadings_ReturnsNormalizedItems`
  - `Parse_EmptyRoadmap_ReturnsNoItems`

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `PrioritizationServiceTests`
- **Methods to add/update**:
  - `Chat_RoadmapRequestWithPath_LoadsRoadmapAndReturnsLinkRecommendations`
  - `Chat_RoadmapRequest_ItemHasNoStory_ListsItemAsUnlinked`
  - `Chat_RoadmapRequest_StoryAlreadyAssigned_DoesNotLinkStoryTwice`
  - `Chat_RoadmapRequest_ProducesBusinessReadableRationaleForEachLink`
  - `Chat_FollowUpQuestionAfterRoadmapLoad_ReusesLoadedRoadmapInMemory`

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `ConsoleHostTests`
- **Methods to add/update**:
  - `HandleInput_RoadmapRequest_ForwardsToPrioritizationService`

### Integration Tests

- **Type**: Integration
- **Required**: No, this scenario is validated with unit tests and runtime smoke checks for command output.
- **Project**: N/A
- **Class**: N/A
- **Methods to add/update**:
  - None

---

## Scenario Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~RoadmapMarkdownParserTests|FullyQualifiedName~PrioritizationServiceTests.Chat_RoadmapRequest|FullyQualifiedName~ConsoleHostTests.HandleInput_RoadmapRequest"`
3. `dotnet format --verify-no-changes .\myownpo.slnx`
4. Run app smoke test by asking in chat to load a roadmap file and verify output readability, cardinality behavior, and follow-up reasoning using the same loaded roadmap.

---

## Scenario Compliance Checks _(mandatory)_

- [ ] No direct file or gateway calls from `ConsoleHost` for roadmap analysis.
- [ ] Matching orchestration remains inside `PrioritizationService` tool-calling flow.
- [ ] Any external calls remain behind dedicated gateway abstractions.
- [ ] Test naming and method style align with existing test suite conventions.
