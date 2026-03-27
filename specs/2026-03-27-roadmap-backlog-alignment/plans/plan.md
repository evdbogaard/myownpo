# Implementation Plan: Roadmap-Backlog Alignment and Context Guidance

**Created**: 2026-03-27
**Spec**: [specs/2026-03-27-roadmap-backlog-alignment/spec.md](specs/2026-03-27-roadmap-backlog-alignment/spec.md)
**Status**: Accepted
**Short Name**: roadmap-backlog-alignment

**Models**: [models.md](specs/2026-03-27-roadmap-backlog-alignment/plans/models.md)

---

## Planning Intent _(mandatory)_

Add roadmap-aware analysis capabilities to the console application so a product owner can load a roadmap markdown file, map roadmap items to backlog stories in New state, identify uncovered roadmap commitments, generate 1-5 candidate story titles for each gap, and receive a project context draft (one vision + three goals).

This rollout keeps the current architecture boundaries: console interaction in `ConsoleHost`, orchestration in services, and external integrations (Azure DevOps + Microsoft Learn MCP) behind gateway interfaces. Roadmap actions are chat-driven (no dedicated roadmap command): user prompts like "please look at current roadmap" trigger tool function-calling in `PrioritizationService`, which loads the roadmap and keeps it in memory for subsequent decisions.

**Non-goals for this plan**:

- No automatic backlog writes in Azure DevOps (suggestion-only output).
- No roadmap editing or persistence beyond reading a provided file.
- No mandatory support for roadmap formats beyond markdown in this release.

---

## Technical Context _(mandatory)_

- **Impacted Areas**: `app/MyOwnPo.App/ConsoleHost.cs`, `app/MyOwnPo.App/Services/`, `app/MyOwnPo.App/Models/`, `app/MyOwnPo.App/Gateways/`, `app/MyOwnPo.App/Program.cs`, and `app/MyOwnPo.App.UnitTests/`.
- **Existing Services/Repositories/Functions**:
  - `IBacklogService` / `BacklogService` already exposes loaded stories.
  - `IPrioritizationService` / `PrioritizationService` already orchestrates LLM chat and tools.
  - `ConsoleHost` already routes commands and chat.
- **Constraints From Instructions**:
  - Console interaction layer calls services only.
  - Services orchestrate business logic and may call gateways.
  - Gateways stay data-access focused (Azure DevOps, Microsoft Learn MCP).
  - Test-first naming style follows existing pattern (`[Method]_[Condition]_[ExpectedResult]`).
  - Formatting and style validated with `.editorconfig` + `dotnet format --verify-no-changes`.
- **Dependencies**:
  - Existing Azure OpenAI chat stack (`Azure.AI.OpenAI`, `Microsoft.Extensions.AI`).
  - `ModelContextProtocol` package with `IMcpToolProvider` for Microsoft Learn MCP tool access.
  - New Microsoft Learn MCP access abstraction (gateway + settings) built on `IMcpToolProvider`.
  - Markdown roadmap parsing utility.

---

## Scenario Plan Map _(mandatory)_

| Scenario                                       | Priority | Plan File                                                                       | Notes                                                                                         |
| ---------------------------------------------- | -------- | ------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| Link Roadmap Items to Existing Backlog Stories | P1       | [scenario-1.md](specs/2026-03-27-roadmap-backlog-alignment/plans/scenario-1.md) | Foundation: parse roadmap, enforce one-story-to-one-roadmap-item rule, explain links          |
| Suggest Missing Stories from Roadmap Gaps      | P1       | [scenario-2.md](specs/2026-03-27-roadmap-backlog-alignment/plans/scenario-2.md) | Gap detection plus 1-5 title suggestions per uncovered item with Microsoft Learn MCP guidance |
| Recommend Project Context from the Roadmap     | P2       | [scenario-3.md](specs/2026-03-27-roadmap-backlog-alignment/plans/scenario-3.md) | Generate one vision and three goals from roadmap themes                                       |

---

## Cross-Scenario Tasks _(when applicable)_

- [ ] Introduce roadmap domain models in `app/MyOwnPo.App/Models/`:
  - `RoadmapItem`
  - `RoadmapStoryLinkRecommendation`
  - `RoadmapGapSuggestion`
  - `RoadmapAnalysisResult`
  - `ProjectContextSuggestion`
  - `LoadedRoadmapState`
  - Model definitions and ownership are documented in [models.md](specs/2026-03-27-roadmap-backlog-alignment/plans/models.md).
- [ ] Extend `IPrioritizationService` / `PrioritizationService` to orchestrate roadmap analysis through chat tool-calling and keep loaded roadmap state in memory.
- [ ] Introduce markdown roadmap loader/parser abstractions:
  - `IRoadmapFileLoader`
  - `RoadmapMarkdownFileLoader`
  - `IRoadmapParser`
  - `RoadmapMarkdownParser`
- [ ] Add Microsoft Learn MCP gateway abstraction in `app/MyOwnPo.App/Gateways/`:
  - `IMicrosoftLearnMcpGateway`
  - `MicrosoftLearnMcpGateway`
  - `MicrosoftLearnMcpSettings`
- [ ] Add `ModelContextProtocol` package reference and wire `IMcpToolProvider` in `Program.cs` for the Microsoft Learn MCP server.
- [ ] Extend `Program.cs` DI wiring for parser/loader/MCP gateway and updated `PrioritizationService` dependencies.
- [ ] Keep `ConsoleHost` chat-first so roadmap analysis is invoked from natural-language prompts, not a dedicated roadmap command.

---

## Delivery Sequence _(mandatory)_

1. **Slice 1 (P1 foundation)**
   Implement tool functions in `PrioritizationService` for roadmap load + roadmap/story linking with rationale and confidence metadata, and keep loaded roadmap state in memory. Enforce cardinality (story links to at most one roadmap item).

2. **Slice 2 (P1 completion)**
   Implement uncovered-item detection and title suggestion generation with Microsoft Learn MCP guidance for Scenario 2 only, using `ModelContextProtocol` and `IMcpToolProvider`. Enforce 1-5 title suggestions per uncovered roadmap item.

3. **Slice 3 (P2)**
   Implement project context synthesis (exactly one vision and exactly three goals), then finalize conversational UX and unit test coverage.

4. **Slice 4 (hardening)**
   Cover edge cases (empty roadmap, ambiguous mapping, no New stories, conflicting themes), tighten confidence heuristics, and run full verification suite.

---

## Global Test Strategy _(mandatory)_

### Unit Tests

| Scenario   | Test Project                | Test Class                      | Method Pattern                                                                                                                                                |
| ---------- | --------------------------- | ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Scenario 1 | `app/MyOwnPo.App.UnitTests` | `RoadmapMarkdownParserTests`    | `Parse_ItemsPresent_ReturnsRoadmapItems`, `Parse_HeadingsOnly_ReturnsEmptyList`                                                                               |
| Scenario 1 | `app/MyOwnPo.App.UnitTests` | `PrioritizationServiceTests`    | `Chat_RoadmapAnalysisRequest_LoadsRoadmapAndReturnsLinks`, `Chat_StoryAlreadyLinked_DoesNotReuseStoryForSecondItem`, `Chat_NoRoadmapMatch_FlagsUnlinkedItems` |
| Scenario 1 | `app/MyOwnPo.App.UnitTests` | `ConsoleHostTests`              | `HandleInput_RoadmapRequest_ForwardsToPrioritizationService`                                                                                                  |
| Scenario 2 | `app/MyOwnPo.App.UnitTests` | `PrioritizationServiceTests`    | `Chat_UncoveredItem_GeneratesOneToFiveStoryTitleSuggestions`, `Chat_MultipleGaps_ProducesAttributableUniqueTitles`                                            |
| Scenario 2 | `app/MyOwnPo.App.UnitTests` | `MicrosoftLearnMcpGatewayTests` | `SearchGuidance_ValidQuery_ReturnsGuidanceSnippets`, `SearchGuidance_GatewayFailure_ReturnsFallbackGuidance`                                                  |
| Scenario 3 | `app/MyOwnPo.App.UnitTests` | `PrioritizationServiceTests`    | `Chat_ContextRequest_ReturnsSingleVisionAndThreeGoals`, `Chat_ConflictingThemes_AvoidsContradictoryGoals`                                                     |
| Scenario 3 | `app/MyOwnPo.App.UnitTests` | `ConsoleHostTests`              | `HandleInput_ContextFromRoadmapRequest_ForwardsToPrioritizationService`                                                                                       |

### Integration Tests

Not required for this plan. Validation is covered with unit tests plus runtime smoke checks.

---

## Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj`
3. `dotnet format --verify-no-changes .\myownpo.slnx`
4. Runtime smoke check:
   - `dotnet run --project .\app\MyOwnPo.App\MyOwnPo.App.csproj`
   - Run `connect`

- Ask in chat: "please look at current roadmap at <path-to-roadmap.md>"
- Ask follow-up questions about links, gaps, and context goals and verify roadmap remains loaded for continued decisions.

---

## Instruction Compliance Checklist _(mandatory)_

- [ ] `ConsoleHost` calls `IPrioritizationService`, not gateways or file I/O directly.
- [ ] Story-link orchestration and gap analysis remain in services, not gateways.
- [ ] Gateways remain focused on external calls only (Azure DevOps, Microsoft Learn MCP).
- [ ] DI wiring changes are limited to `Program.cs`.
- [ ] Security review includes no secrets in logs and MCP credentials loaded from configuration/user secrets.
- [ ] Unit tests are planned for all acceptance paths.
- [ ] Formatting and style checks include `.editorconfig` and `dotnet format --verify-no-changes`.
- [ ] Microsoft Learn MCP integration uses `ModelContextProtocol` with `IMcpToolProvider`.

---

## Unresolved Blockers _(mandatory)_

None

---
