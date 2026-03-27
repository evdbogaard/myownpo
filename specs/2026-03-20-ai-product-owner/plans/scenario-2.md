# Scenario Plan: Get Prioritization Suggestions

**Created**: 2026-03-20
**Spec**: [specs/2026-03-20-ai-product-owner/spec.md](specs/2026-03-20-ai-product-owner/spec.md)
**Master Plan**: [specs/2026-03-20-ai-product-owner/plans/plan.md](specs/2026-03-20-ai-product-owner/plans/plan.md)
**Scenario Priority**: P1
**Scenario Index**: 2

---

## Scenario Goal _(mandatory)_

After the backlog has been ingested, allow the team member to request prioritization suggestions from the AI Product Owner agent. The agent — powered by `Microsoft.Extensions.AI` (`IChatClient` with function calling) — analyzes all stories and returns a ranked list where every story has a justification. The team member can ask follow-up questions ("why is X above Y?") naturally in conversation, and re-request suggestions after providing feedback. The backlog is never modified.

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
- **Target Behavior**: The console loop is upgraded to a conversational interface powered by `Microsoft.Extensions.AI` (`IChatClient` with `UseFunctionInvocation()` middleware for automatic tool calling). The AI Product Owner agent behavior is defined via a system prompt and registered tools inside `PrioritizationService`. The team member types natural language messages (e.g., "suggest priorities", "why is story X above Y?"), which are sent via `IChatClient`. The agent uses its tools and LLM reasoning to produce prioritization suggestions, explanations, and re-rankings. Duplicate stories are flagged when detected. The backlog in Azure DevOps is never written to.
- **Architecture Boundaries**:
  - `ConsoleHost` becomes a conversational loop: read user input → send to `IPrioritizationService.Chat()` → display response. Accepts `TextReader`/`TextWriter` for testability (defaults to `Console.In`/`Console.Out`).
  - `PrioritizationService` manages the `IChatClient` instance, conversation history (`List<ChatMessage>`), system prompt, and tool registration via `AIFunctionFactory`. No separate agent definition class.
  - `IChatClient` is configured in DI with `UseFunctionInvocation()` middleware to automatically handle tool call loops (LLM requests tool call → middleware invokes function → sends result back → LLM produces final response).
  - The `GetBacklogStories` tool is the only registered tool — it returns stories from `IBacklogService.GetStories()` as JSON. The LLM handles all prioritization, explanations, and re-suggestions through conversation context and system prompt instructions.
  - The actual Azure DevOps backlog is never written to.
- **Dependencies**: `Microsoft.Agents.AI.OpenAI` 1.0.0-rc4 (transitively provides `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.OpenAI`, `Microsoft.Extensions.AI.Abstractions`, `Microsoft.Agents.AI`), `Azure.AI.OpenAI` 2.3.0-beta.1 (Azure OpenAI SDK client), `IBacklogService` from Scenario 1.

---

## Implementation Tasks _(mandatory)_

- [x] **TASK-2-01**: Define `SuggestedStoryRank` model with properties: `StoryId` (string), `StoryTitle` (string), `Rank` (int), `Justification` (string). See [models.md](specs/2026-03-20-ai-product-owner/plans/models.md#suggestedstoryrank) for full definition.
  - **Layer**: Models
  - **Reason**: Represents one story's position in the suggested order.

- [x] **TASK-2-02**: Define `PrioritizationSuggestion` model with properties: `Rankings` (list of `SuggestedStoryRank`), `Created` (DateTimeOffset), `DuplicateWarnings` (list of string). See [models.md](specs/2026-03-20-ai-product-owner/plans/models.md#prioritizationsuggestion) for full definition.
  - **Layer**: Models
  - **Reason**: Complete response type for the prioritization feature, including duplicate flags (FR-011).

- [x] **TASK-2-03**: Define the AI Product Owner agent behavior inside `PrioritizationService`.
  - **System prompt**: Product Owner persona, suggestion-only mode (FR-006), prioritization expertise, instruction to produce ranked output with justifications, instruction to flag potential duplicates, instruction to refuse modification requests. Stored as a `static readonly string` using raw string literal.
  - **Tool**: Register `GetBacklogStories` via `AIFunctionFactory.Create()` in `ChatOptions.Tools`. The tool calls `IBacklogService.GetStories()` and returns all story fields as JSON.
  - **IChatClient**: Injected via constructor. Configured in DI with `UseFunctionInvocation()` middleware that auto-handles tool call loops.
  - **Layer**: Services
  - **Reason**: No separate agent definition class needed. `PrioritizationService` directly manages the `IChatClient`, conversation history, system prompt, and tool registration. The `UseFunctionInvocation()` middleware handles the tool-calling loop automatically.

- [x] **TASK-2-04**: Define `IPrioritizationService` interface with method:
  - `Task<string> Chat(string userMessage)` — the single public entry point for all conversational interaction. `ConsoleHost` sends all natural language input here and displays the returned response.
  - **Layer**: Services
  - **Reason**: Business-layer abstraction for prioritization operations. The LLM handles intent detection internally via the system prompt; the single `Chat()` method covers all interactions.

- [x] **TASK-2-05**: Implement `PrioritizationService : IPrioritizationService`.
  - Holds `IChatClient`, `IBacklogService`, `List<ChatMessage>` (conversation history), and `ChatOptions` (with tools).
  - Constructor: Initializes history with system prompt. Creates `ChatOptions` with `GetBacklogStories` tool via `AIFunctionFactory.Create()`.
  - `Chat(string)`: Adds user message to history → trims history to 20 messages (keeping system prompt at index 0) → calls `_chatClient.GetResponseAsync(_history, _chatOptions)` → adds response via `_history.AddMessages(response)` → returns `response.Text`.
  - Only one tool registered: `GetBacklogStories` — returns stories as serialized JSON. The LLM handles all prioritization, explanations, and re-suggestions through conversation context. No separate `SuggestPriorities`/`ExplainRanking`/`ResuggestPriorities` tools.
  - Edge cases (< 2 stories, empty backlog, duplicates, refuse-to-modify) handled by the system prompt instructions.
  - **Layer**: Services
  - **Reason**: Single class handles everything: system prompt, tool registration, conversation history, and LLM interaction. The `UseFunctionInvocation()` middleware (configured in DI) handles tool call loops automatically.

- [x] **TASK-2-06**: Upgrade `ConsoleHost` from command-based to conversational agent loop.
  - Primary constructor accepts `IBacklogService`, `IPrioritizationService`, `TextReader`, `TextWriter`. Secondary constructor defaults to `Console.In`/`Console.Out` for production use.
  - Retain `connect`, `refresh`, `help`, `exit` as recognized commands that bypass the agent and call `IBacklogService` directly.
  - `default` case: guards with `_backlogService.GetStories().Count == 0` → displays "No backlog loaded. Use 'connect' to load stories first." Otherwise, forwards input to `_prioritizationService.Chat(command)` and displays the response.
  - Help text updated to mention conversational capabilities ("Or type a question to chat with the AI Product Owner.").
  - All `Console.ReadLine()`/`Console.WriteLine()` replaced with `_input`/`_output` fields for testability.
  - **Layer**: Console Interaction
  - **Reason**: The conversational loop leverages `IChatClient` conversation history, making follow-up questions and re-suggestions natural. `TextReader`/`TextWriter` injection enables unit testing without `Console.SetIn()`/`Console.SetOut()`.

- [x] **TASK-2-07**: Register `IChatClient` and `IPrioritizationService` in the DI container in `Program.cs`. Configure Azure OpenAI settings (endpoint, deployment name) from `appsettings.json` and API key from user secrets, both bound to `AzureOpenAiSettings`. See [models.md](specs/2026-03-20-ai-product-owner/plans/models.md#azureopenaisettings).
  - `AzureOpenAiSettings` bound from `configuration.GetSection("AzureOpenAi")` with `.ValidateDataAnnotations().ValidateOnStart()`.
  - `IChatClient` registered as singleton: `new AzureOpenAIClient(endpoint, ApiKeyCredential)` → `.GetChatClient(deploymentName)` → `.AsIChatClient()` → `.AsBuilder().UseFunctionInvocation().Build()`.
  - `IPrioritizationService` → `PrioritizationService` registered as singleton.
  - **Layer**: Composition Root
  - **Reason**: New services need DI registration. Azure OpenAI API key must come from user secrets. `UseFunctionInvocation()` middleware enables automatic tool call handling.

---

## Files To Alter _(mandatory)_

| File                                                    | Change Type | Why                                                                                                        |
| ------------------------------------------------------- | ----------- | ---------------------------------------------------------------------------------------------------------- |
| `Models/SuggestedStoryRank.cs`                          | Add         | Ranked story position model                                                                                |
| `Models/PrioritizationSuggestion.cs`                    | Add         | Full suggestion response model                                                                             |
| `Gateways/AzureOpenAiSettings.cs`                       | Add         | Azure OpenAI configuration (endpoint, deployment, API key) with data annotation validation                 |
| `Services/IPrioritizationService.cs`                    | Add         | Prioritization service interface                                                                           |
| `Services/PrioritizationService.cs`                     | Add         | System prompt, tool registration, conversation history, `IChatClient` orchestration                        |
| `ConsoleHost.cs`                                        | Modify      | Upgrade to conversational agent loop                                                                       |
| `Program.cs`                                            | Modify      | Register agent, IChatClient, and PrioritizationService in DI; configure Azure OpenAI from appsettings.json |
| `app/MyOwnPo.App/MyOwnPo.App.csproj`                                        | Modify      | Add `Microsoft.Agents.AI.OpenAI` 1.0.0-rc4 and `Azure.AI.OpenAI` 2.3.0-beta.1 NuGet packages             |
| `app/MyOwnPo.App.UnitTests/PrioritizationServiceTests.cs` | Add         | Unit tests for PrioritizationService                                                                       |
| `app/MyOwnPo.App.UnitTests/ConsoleHostTests.cs`           | Add         | Tests for conversational routing                                                                           |

---

## Technical Questions _(mandatory)_

- **Agent response format**: Resolved — `Chat()` returns raw LLM text (`response.Text`). `SuggestedStoryRank` and `PrioritizationSuggestion` models exist for future structured output parsing but are not actively used in MVP.
- **Duplicate detection algorithm**: Resolved — agent-based only. The system prompt instructs the LLM to flag potential duplicates when detected. No programmatic title similarity fallback in MVP.
- **Agent tool granularity**: Resolved — single `GetBacklogStories` tool registered via `AIFunctionFactory.Create()`. The LLM decides when to call it. No separate `SuggestPriorities`/`ExplainRanking`/`ResuggestPriorities` tools — the LLM handles all reasoning via conversation context.
- **Conversation history limits**: Resolved — bounded at 20 messages. When exceeded, oldest non-system messages are trimmed from the front (system prompt at index 0 is always preserved).
- **Agent framework**: Resolved — `Microsoft.Agents.Builder` doesn't exist on NuGet in the expected form and targets M365/Teams bot routing. Replaced with `Microsoft.Agents.AI.OpenAI` 1.0.0-rc4 + `Azure.AI.OpenAI` 2.3.0-beta.1 + `Microsoft.Extensions.AI` (transitive). Agent behavior defined in `PrioritizationService` using `IChatClient` directly.

---

## Testing Criteria _(mandatory)_

### Unit Tests

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `PrioritizationServiceTests`
- **Methods implemented**:
  - `Chat_GeneralMessage_ReturnsAgentResponse`
  - `Chat_CallsGetResponseAsyncWithUserMessage`
  - `Chat_FirstMessage_IncludesSystemPrompt`
  - `Chat_MultipleCalls_AccumulatesHistory`
  - `Chat_NullResponseText_ReturnsEmptyString`
  - `Chat_ConfiguresChatOptionsWithTools`

- **Class**: `ConsoleHostTests`
- **Methods implemented**:
  - `HandleInput_ConnectCommand_CallsBacklogService`
  - `HandleInput_NaturalLanguage_ForwardsToAgent`
  - `HandleInput_NaturalLanguageWithNoBacklog_PromptsToConnect`
  - `HandleInput_ExitCommand_Exits`
  - `HandleInput_HelpCommand_ShowsHelp`
  - `HandleInput_RefreshCommand_CallsBacklogService`

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

1. `dotnet build .\myownpo.slnxx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~PrioritizationService|FullyQualifiedName~ConsoleHost"`
3. `dotnet test .\tests\MyOwnPo.IntegrationTests\MyOwnPo.IntegrationTests.csproj --filter "FullyQualifiedName~Prioritization"`
4. `dotnet format --verify-no-changes .\myownpo.slnxx`
5. Manual smoke test: connect to Azure DevOps backlog, type "suggest priorities for the backlog", verify all stories appear ranked with justifications. Ask "why is story X above story Y?", verify explanation. Confirm the Azure DevOps backlog is unmodified.

---

## Scenario Compliance Checks _(mandatory)_

- [x] No direct `IChatClient` or `IBacklogGateway` calls from `ConsoleHost` — all go through `IPrioritizationService` and `IBacklogService`.
- [x] Service boundary respected: `PrioritizationService` orchestrates all LLM interactions; `IChatClient` with `UseFunctionInvocation()` middleware handles tool call loops internally.
- [x] The actual Azure DevOps backlog is never written to — the only registered tool (`GetBacklogStories`) is read-only.
- [x] Security: Azure OpenAI API key is loaded from user secrets (never `appsettings.json`), never logged or displayed. User story content sent to the LLM is acknowledged as a data-flow concern (team member is aware their backlog data is sent to an LLM).
- [x] Tests and naming conventions aligned with project standards.
- [x] `ConsoleHost` testable via `TextReader`/`TextWriter` injection (no `Console.SetIn`/`SetOut` needed).
- [x] `dotnet format --verify-no-changes` passes.

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

### Session 2026-03-27 — Implementation

- **Framework change** (CRITICAL): `Microsoft.Agents.Builder` 1.0.0-rc4 doesn't exist on NuGet and targets M365/Teams bot routing, not console apps. → **Fix**: Replaced with `Microsoft.Agents.AI.OpenAI` 1.0.0-rc4 + `Azure.AI.OpenAI` 2.3.0-beta.1. Agent behavior defined via system prompt + tools in `PrioritizationService` using `IChatClient` directly with `UseFunctionInvocation()` middleware.
- **Architecture simplification**: Removed `Agents/ProductOwnerAgentDefinition.cs`. Removed separate `SuggestPriorities`/`ExplainRanking`/`ResuggestPriorities` tools — single `GetBacklogStories` tool; LLM handles all reasoning via conversation context and system prompt.
- **Testability**: `ConsoleHost` refactored to accept `TextReader`/`TextWriter` via primary constructor for unit testing. Added `AzureOpenAiSettings` to `Gateways/` with `[Required]`/`[Url]` data annotations.
- **Test alignment**: Updated `PrioritizationServiceTests` and `ConsoleHostTests` method names to reflect actual Chat()-based API (no separate Suggest/Explain/Resuggest methods). All tasks marked completed.
