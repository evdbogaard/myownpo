# Scenario Plan: Get Prioritization Suggestions

**Created**: 2026-03-20
**Spec**: [specs/2026-03-20-ai-product-owner/spec.md](specs/2026-03-20-ai-product-owner/spec.md)
**Master Plan**: [specs/2026-03-20-ai-product-owner/plans/plan.md](specs/2026-03-20-ai-product-owner/plans/plan.md)
**Scenario Priority**: P1
**Scenario Index**: 2

---

## Scenario Goal _(mandatory)_

After the backlog has been ingested, allow the team member to request prioritization suggestions from the AI Product Owner agent. The agent — built on the Microsoft Agent Framework — analyzes all stories and returns a ranked list where every story has a justification. The team member can ask follow-up questions ("why is X above Y?") naturally in conversation, and re-request suggestions after providing feedback. The backlog is never modified.

---

## Acceptance Traceability _(mandatory)_

- **Scenario Acceptance Criteria**:
  1. **Given** an ingested backlog with 3 or more stories, **When** the team member asks for prioritization suggestions, **Then** the AI Product Owner presents all stories in a suggested priority order, each with a justification.
  2. **Given** a set of prioritization suggestions, **When** the team member questions a specific ranking, **Then** the AI Product Owner explains its reasoning and can offer an alternative if the team member provides additional context.
  3. **Given** a set of prioritization suggestions, **When** the team member takes no action, **Then** the actual backlog remains completely unchanged.
- **Related Requirements**: FR-004, FR-005, FR-006, FR-007
- **Related Edge Cases**: Single story (not enough to prioritize), refuse-to-modify requests, duplicate detection (FR-011)

---

## Technical Context _(mandatory)_

- **Current Behavior**: After Scenario 1, the app can ingest stories from Azure DevOps and display a summary. No prioritization or agent integration exists.
- **Target Behavior**: The console loop is upgraded to a conversational interface powered by the Microsoft Agent Framework. The AI Product Owner agent is defined with a system prompt (Product Owner persona, suggestion-only mode, prioritization expertise) and registered tools (`GetBacklogStories`). The team member types natural language messages (e.g., "suggest priorities", "why is story X above Y?"), which are sent to the agent. The agent uses its tools and LLM reasoning to produce prioritization suggestions, explanations, and re-rankings. Duplicate stories are flagged when detected. The backlog in Azure DevOps is never written to.
- **Architecture Boundaries**:
  - `ConsoleHost` becomes a conversational loop: read user input → send to agent → display agent response.
  - The AI Product Owner agent is defined using `Microsoft.Agents.Builder` with a system prompt and tools. The agent internally calls `IBacklogService` methods via registered tools.
  - `IPrioritizationService` / `PrioritizationService` orchestrates the agent invocation and structures the response into `PrioritizationSuggestion`. It holds the agent instance and manages the conversation thread.
  - The agent uses an `IChatClient` (provided by the LLM provider package) under the hood.
  - The actual Azure DevOps backlog is never written to.
- **Dependencies**: `Microsoft.Agents.Builder` 1.0.0-rc4 / `Microsoft.Agents.Core` (Agent Framework), `Azure.AI.OpenAI` + `Microsoft.Extensions.AI.OpenAI` (Azure OpenAI LLM provider), `IBacklogService` from Scenario 1.

---

## Implementation Tasks _(mandatory)_

- [ ] **TASK-2-01**: Define `SuggestedStoryRank` model with properties: `StoryId` (string), `StoryTitle` (string), `Rank` (int), `Justification` (string). See [models.md](specs/2026-03-20-ai-product-owner/plans/models.md#suggestedstoryrank) for full definition.
  - **Layer**: Models
  - **Reason**: Represents one story's position in the suggested order.

- [ ] **TASK-2-02**: Define `PrioritizationSuggestion` model with properties: `Rankings` (list of `SuggestedStoryRank`), `Created` (DateTimeOffset), `DuplicateWarnings` (list of string). See [models.md](specs/2026-03-20-ai-product-owner/plans/models.md#prioritizationsuggestion) for full definition.
  - **Layer**: Models
  - **Reason**: Complete response type for the prioritization feature, including duplicate flags (FR-011).

- [ ] **TASK-2-03**: Define the AI Product Owner agent using the Microsoft Agent Framework.
  - Create an `AgentDefinition` (or equivalent) with:
    - **System prompt**: Describe the Product Owner persona, suggestion-only mode (FR-006), prioritization expertise, instruction to produce JSON-structured ranked output with justifications, instruction to flag potential duplicates, instruction to refuse modification requests.
    - **Tools**: Register `GetBacklogStories` (calls `IBacklogService.GetStories()`) as an agent tool so the agent can retrieve backlog data when reasoning.
  - Configure the agent with an `IChatClient` from Azure OpenAI (`Azure.AI.OpenAI` + `Microsoft.Extensions.AI.OpenAI`).
  - **Layer**: Agents
  - **Reason**: The agent is the core AI component. The Microsoft Agent Framework handles conversation history, tool calling, and LLM interaction — replacing manual prompt building and `ILlmService`.

- [ ] **TASK-2-04**: Define `IPrioritizationService` interface with method:
  - `Task<string> Chat(string userMessage)` — the single public entry point for all conversational interaction. `ConsoleHost` sends all natural language input here and displays the returned response.
  - **Layer**: Services
  - **Reason**: Business-layer abstraction for prioritization operations. The agent handles intent detection internally; specific operations (suggest, explain, re-suggest) are exposed as agent tools (function calling), not as public service methods.

- [ ] **TASK-2-05**: Implement `PrioritizationService : IPrioritizationService`.
  - Hold a reference to the agent instance and manage the conversation thread (chat history).
  - `Chat`: Forward the user message to the agent and return the agent's response text. This is the only public method — all intent detection happens inside the agent.
  - Register the following as **agent tools** (function calling) so the agent can invoke them when it determines the user's intent:
    - `SuggestPriorities(IReadOnlyList<UserStory> stories)` — if fewer than 2 stories, short-circuit and return a meaningful message without calling the LLM; otherwise rank all stories, parse into `SuggestedStoryRank` list, detect potential duplicates by title similarity and add warnings.
    - `ExplainRanking(string storyIdA, string storyIdB)` — explain a specific ranking pair using conversation history context.
    - `ResuggestPriorities(IReadOnlyList<UserStory> stories, string feedback)` — re-rank with the team member's feedback incorporated.
  - Validate that the agent's ranked response covers all stories; if not, flag missing ones.
  - **Layer**: Services
  - **Reason**: Orchestrates agent interactions and structures responses. `Chat()` is the single entry point; the Agent Framework handles intent detection, tool calling, and conversation history internally.

- [ ] **TASK-2-06**: Upgrade `ConsoleHost` from command-based to conversational agent loop.
  - The main loop reads user input and sends it to `IPrioritizationService.Chat` (or specific methods for structured commands like `connect`, `refresh`, `exit`).
  - Retain `connect`, `refresh`, `help`, `exit` as recognized commands that bypass the agent and call `IBacklogService` directly.
  - All other input is forwarded to the agent as natural language. The agent handles: prioritization requests, follow-up questions, re-suggestion requests, and refuse-to-modify responses.
  - Display agent responses with formatting (ranked lists, justifications, warnings).
  - **Layer**: Console Interaction
  - **Reason**: The conversational loop leverages the Agent Framework's built-in conversation history, making follow-up questions and re-suggestions natural without explicit `why` or `resuggest` commands.

- [ ] **TASK-2-07**: Register agent, `IChatClient`, and `IPrioritizationService` in the DI container in `Program.cs`. Configure Azure OpenAI settings (endpoint, deployment name) from `appsettings.json` and API key from user secrets, both bound to `AzureOpenAiSettings`. See [models.md](specs/2026-03-20-ai-product-owner/plans/models.md#azureopenaisettings).
  - **Layer**: Composition Root
  - **Reason**: New services and the agent need DI registration. Azure OpenAI API key must come from user secrets.

---

## Files To Alter _(mandatory)_

| File                                                    | Change Type | Why                                                                                                        |
| ------------------------------------------------------- | ----------- | ---------------------------------------------------------------------------------------------------------- |
| `Models/SuggestedStoryRank.cs`                          | Add         | Ranked story position model                                                                                |
| `Models/PrioritizationSuggestion.cs`                    | Add         | Full suggestion response model                                                                             |
| `Agents/ProductOwnerAgentDefinition.cs`                 | Add         | Agent definition with system prompt and tool registrations (Microsoft Agent Framework)                     |
| `Services/IPrioritizationService.cs`                    | Add         | Prioritization service interface                                                                           |
| `Services/PrioritizationService.cs`                     | Add         | Agent orchestration + response parsing                                                                     |
| `ConsoleHost.cs`                                        | Modify      | Upgrade to conversational agent loop                                                                       |
| `Program.cs`                                            | Modify      | Register agent, IChatClient, and PrioritizationService in DI; configure Azure OpenAI from appsettings.json |
| `myownpo.csproj`                                        | Modify      | Add Microsoft.Agents.Builder and LLM provider NuGet packages                                               |
| `tests/MyOwnPo.UnitTests/PrioritizationServiceTests.cs` | Add         | Unit tests for PrioritizationService                                                                       |
| `tests/MyOwnPo.UnitTests/ConsoleHostTests.cs`           | Add         | Tests for conversational routing                                                                           |

---

## Technical Questions _(mandatory)_

- **Agent response format**: Resolved — enforce structured output via Agent Framework structured output when available; fallback to prompt-constrained JSON plus one retry on parse failure.
- **Duplicate detection algorithm**: Simple title similarity (e.g., Levenshtein distance or normalized containment) or agent-based? Recommendation: include a duplicate-detection instruction in the agent's system prompt so the LLM flags duplicates naturally; supplement with simple title similarity as a fallback.
- **Agent tool granularity**: Should backlog data be passed to the agent as tool results (agent calls `GetBacklogStories` tool) or pre-loaded into the user message? Recommendation: register as tools — this lets the agent decide when to fetch stories and keeps token usage efficient for follow-up questions where the agent already has context.
- **Conversation history limits**: Resolved — use bounded conversation history in MVP by retaining the latest 20 turns; when threshold is exceeded, summarize older turns into a single context note.

---

## Testing Criteria _(mandatory)_

### Unit Tests

- **Type**: Unit
- **Project**: `tests/MyOwnPo.UnitTests`
- **Class**: `PrioritizationServiceTests`
- **Methods to add/update**:
  - `Suggest_ThreeStories_ReturnsAllThreeRankedWithJustifications`
  - `Suggest_StoriesWithDuplicateTitles_IncludesDuplicateWarnings`
  - `Suggest_AgentReturnsMalformedResponse_RetriesOrReportsError`
  - `Suggest_AgentOmitsStory_FlagsMissingStory`
  - `Suggest_AnyRequest_DoesNotModifyBacklog`
  - `ExplainRanking_ValidPair_ReturnsExplanation`
  - `Resuggest_WithFeedback_ProducesUpdatedOrder`
  - `Chat_GeneralMessage_ReturnsAgentResponse`

- **Class**: `ConsoleHostTests`
- **Methods to add/update**:
  - `HandleInput_ConnectCommand_CallsBacklogService`
  - `HandleInput_NaturalLanguage_ForwardsToAgent`
  - `HandleInput_SuggestWithNoBacklog_PromptsToConnect`
  - `HandleInput_SuggestWithSingleStory_ReportsMinimumRequired`
  - `HandleInput_ModifyRequest_ReturnsSuggestionOnlyMessage`

### Integration Tests

- **Type**: Integration
- **Required**: Yes — verifies that the full chain from stories → agent tools → LLM → parsed suggestion works.
- **Project**: `tests/MyOwnPo.IntegrationTests`
- **Class**: `PrioritizationIntegrationTests`
- **Methods to add/update**:
  - `SuggestPriorities_IngestedBacklog_ReturnsCompleteRankedList`
  - `ConversationalFollowUp_AfterSuggest_ReturnsCoherentExplanation`

---

## Scenario Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.sln`
2. `dotnet test .\tests\MyOwnPo.UnitTests\MyOwnPo.UnitTests.csproj --filter "FullyQualifiedName~PrioritizationService|FullyQualifiedName~ConsoleHost"`
3. `dotnet test .\tests\MyOwnPo.IntegrationTests\MyOwnPo.IntegrationTests.csproj --filter "FullyQualifiedName~Prioritization"`
4. `dotnet format --verify-no-changes .\myownpo.sln`
5. Manual smoke test: connect to Azure DevOps backlog, type "suggest priorities for the backlog", verify all stories appear ranked with justifications. Ask "why is story X above story Y?", verify explanation. Confirm the Azure DevOps backlog is unmodified.

---

## Scenario Compliance Checks _(mandatory)_

- [ ] No direct `IChatClient` or `IBacklogGateway` calls from `ConsoleHost` — all go through `IPrioritizationService` and `IBacklogService`.
- [ ] Service boundary respected: `PrioritizationService` orchestrates agent interactions and response parsing; the Agent Framework handles LLM calls and tool invocations internally.
- [ ] The actual Azure DevOps backlog is never written to — the agent has no tools that modify work items. Verified in integration tests and by code review.
- [ ] Security: Azure OpenAI API key is loaded from user secrets (never `appsettings.json`), never logged or displayed. User story content sent to the LLM is acknowledged as a data-flow concern (team member is aware their backlog data is sent to an LLM).
- [ ] Tests and naming conventions aligned with project standards.

---

## Revisions

### Session 2026-03-24

- **A1** (Ambiguity / HIGH): `IPrioritizationService` had four public methods but `ConsoleHost` only called `Chat()`. → **Fix**: `Chat()` is now the single public method; `Suggest`/`ExplainRanking`/`Resuggest` documented as agent tools (function calling) in TASK-2-05.
- **U3** (Underspec / HIGH): Single-story edge case had tests but no implementation task. → **Fix**: Added single-story guard to `SuggestPriorities` agent tool in TASK-2-05 (short-circuits before LLM call).
- **U5** (Underspec / MEDIUM): `HandleInput_ModifyRequest_ReturnsSuggestionOnlyMessage` was in master plan but missing from scenario-2 testing criteria. → **Fix**: Added to `ConsoleHostTests` testing criteria.
- **Blocker resolution**: LLM provider set to Azure OpenAI (`Azure.AI.OpenAI` + `Microsoft.Extensions.AI.OpenAI`). Agent Framework version set to `Microsoft.Agents.Builder` 1.0.0-rc4. Azure OpenAI config via `appsettings.json`. Updated Dependencies, TASK-2-03, TASK-2-07, files table, and compliance checks.

### Session 2026-03-24 #2

- **I1** (Instruction / CRITICAL): Scenario task layer labels used custom taxonomy. → **Fix**: Added explicit Layer Label Mapping that maps existing labels to template-required taxonomy.
- **C1** (Consistency / HIGH): Integration test naming needed canonical alignment with master plan. → **Fix**: Kept scenario naming as canonical and aligned master plan names to scenario-2 method names.
- **S1** (Spec-coverage / HIGH): AC3 (no backlog modification) lacked an explicit test method. → **Fix**: Added `Suggest_AnyRequest_DoesNotModifyBacklog` to `PrioritizationServiceTests` methods.
- **U1** (Underspec / MEDIUM): Missing config files in Files To Alter for Azure OpenAI settings. → **Skipped**: User chose to treat config/settings scope as implicit in existing `Program.cs` entry.
- **A1** (Ambiguity / MEDIUM): Core implementation choices for response format/history remained open recommendations. → **Fix**: Converted to explicit MVP decisions for structured output and bounded history strategy.
