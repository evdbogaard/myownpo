# Implementation Plan: AI Product Owner — Backlog Suggestions

**Created**: 2026-03-20
**Spec**: [specs/2026-03-20-ai-product-owner/spec.md](specs/2026-03-20-ai-product-owner/spec.md)
**Status**: Accepted
**Short Name**: ai-product-owner

---

## Planning Intent _(mandatory)_

Build a read-only, suggestion-only MVP console application that connects to an Azure DevOps backlog, ingests user stories, and produces AI-powered prioritization suggestions using `Microsoft.Extensions.AI` (`IChatClient` with function calling via `UseFunctionInvocation()` middleware). The system never modifies the backlog. The console app runs a conversational agent loop where the team member interacts with the AI Product Owner agent via natural language.

**Non-goals for this plan**:

- No web UI, API, or hosted service — console only.
- No direct backlog modification — strictly read-only.
- No support for non-Azure DevOps backlog providers (e.g., Jira, GitHub Issues) — MVP connects to Azure DevOps only. The architecture supports adding providers later via the `IBacklogGateway` abstraction.
- No multi-user support (single PAT, single team member session).

---

## Technical Context _(mandatory)_

- **Impacted Areas**: The entire application is greenfield. The current codebase is an empty .NET 10 console app (`Program.cs` with "Hello, World!"). All layers must be created: models, services, gateways, console interaction, and test projects.
- **Existing Services/Repositories/Functions**: None — greenfield.
- **Constraints From Instructions**:
  - The console interaction layer (analogous to controllers) must call services, never gateways/repositories directly.
  - Services may call gateways/repositories.
  - Gateways/repositories must remain data-access focused.
  - `Nullable` is enabled; use nullable reference types throughout.
  - `ImplicitUsings` is enabled.
  - Tab indentation, 4-space tab width (per `.editorconfig`).
  - `dotnet format --verify-no-changes` must pass.
- **Dependencies**:
  - `Microsoft.Agents.AI.OpenAI` 1.0.0-rc4 — bridges Azure OpenAI SDK to `IChatClient`. Transitively provides `Microsoft.Extensions.AI` >= 10.3.0, `Microsoft.Extensions.AI.OpenAI` >= 10.3.0, `Microsoft.Extensions.AI.Abstractions`, `Microsoft.Agents.AI`.
  - `Azure.AI.OpenAI` 2.3.0-beta.1 — Azure OpenAI SDK client. Must be added explicitly (not transitive from `Microsoft.Agents.AI.OpenAI`).
  - `Microsoft.TeamFoundation.WorkItemTracking.WebApi` + `Microsoft.VisualStudio.Services.Client` — Azure DevOps REST API client for reading work items.
  - `Microsoft.Extensions.DependencyInjection` for DI.
  - `Microsoft.Extensions.Hosting` for host builder (DI + config + secrets).
  - `Microsoft.Extensions.Configuration.UserSecrets` — to load the Azure DevOps PAT from user secrets.
  - A test framework: `xunit` + `Moq` or `NSubstitute`.

---

## Scenario Plan Map _(mandatory)_

| Scenario                             | Priority | Plan File                                                              | Notes                                                                                            |
| ------------------------------------ | -------- | ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| Connect to an Existing Backlog       | P1       | [scenario-1.md](specs/2026-03-20-ai-product-owner/plans/scenario-1.md) | Foundation — backlog ingestion, summary, refresh, edge cases (empty, inaccessible, >100 stories) |
| Get Prioritization Suggestions       | P1       | [scenario-2.md](specs/2026-03-20-ai-product-owner/plans/scenario-2.md) | Core value — LLM-powered priority ranking with justifications, follow-up Q&A                     |
| Get Suggestions with Project Context | P2       | [scenario-3.md](specs/2026-03-20-ai-product-owner/plans/scenario-3.md) | Enhancement — context-aware suggestions, context management                                      |

---

## Cross-Scenario Tasks _(when applicable)_

- [ ] **Define domain models**: `UserStory`, `BacklogConnection`, `PrioritizationSuggestion`, `SuggestedStoryRank`, `ProjectContext` in a `Models/` folder. Define `AzureDevOpsSettings` in `Gateways/`. See [models.md](specs/2026-03-20-ai-product-owner/plans/models.md) for full model definitions.
- [ ] **Create `IBacklogGateway` abstraction** and `AzureDevOpsBacklogGateway` implementation in a `Gateways/` folder. The gateway uses the Azure DevOps REST API client with PAT authentication.
- [ ] **Define the AI Product Owner agent** behavior inside `PrioritizationService` using `IChatClient` with `UseFunctionInvocation()` middleware. The agent is configured with a system prompt (Product Owner persona), tools registered via `AIFunctionFactory.Create()`, and conversation history managed in a `List<ChatMessage>`. No separate agent definition class.
- [ ] **Set up DI and hosting** in `Program.cs` using `Microsoft.Extensions.Hosting` — register all services, gateways, the agent, bind configuration from `appsettings.json`, and configure user secrets for the PAT.
- [ ] **Create `appsettings.json`** with the Azure DevOps configuration (organization URL, project name, optional area path) and Azure OpenAI configuration (endpoint, deployment name). The Azure DevOps PAT and Azure OpenAI API key are loaded from user secrets (not `appsettings.json`).
- [ ] **Create the conversational console loop** (`ConsoleHost` or similar) that sends user messages to the agent and displays agent responses. The agent dispatches tool calls to services internally.
- [ ] **Create test projects**: `app/MyOwnPo.App.UnitTests/MyOwnPo.App.UnitTests.csproj` and optionally `tests/MyOwnPo.IntegrationTests/MyOwnPo.IntegrationTests.csproj`.
- [ ] **Add NuGet packages** to `app/MyOwnPo.App/MyOwnPo.App.csproj`: `Microsoft.Agents.AI.OpenAI` 1.0.0-rc4, `Azure.AI.OpenAI` 2.3.0-beta.1, Azure DevOps client libraries, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.UserSecrets`.
- [ ] **Handle the "refuse to modify" edge case** in the agent's system prompt — instruct the agent to decline modification requests and offer suggestions instead (FR-006).

---

## Delivery Sequence _(mandatory)_

1. **Slice 1 — Domain Models + Azure DevOps Gateway + Ingestion (Scenario 1 core)**
   Create all domain models. Implement `IBacklogGateway` and `AzureDevOpsBacklogGateway` (PAT auth, WIQL query filtering to active User Stories only — excludes Closed and Removed). Implement `IBacklogService` and `BacklogService` (connect, ingest, summarize). Set up DI, user secrets, and `appsettings.json` in `Program.cs`. Create a minimal console loop with `connect` and `refresh` commands. Create unit tests for `BacklogService` and `AzureDevOpsBacklogGateway`.

2. **Slice 2 — AI Agent + Prioritization (Scenario 2 core)**
   Define the AI Product Owner agent behavior inside `PrioritizationService` using `IChatClient` with `UseFunctionInvocation()` middleware, a system prompt, and `AIFunctionFactory.Create()` for tool registration. Register `GetBacklogStories` as the agent tool. Implement `IPrioritizationService` / `PrioritizationService` with conversation history management. Replace the command-based console loop with a conversational agent loop (`ConsoleHost` accepts `TextReader`/`TextWriter` for testability). Create unit tests for `PrioritizationService` (with mocked `IChatClient`) and `ConsoleHost`.

3. **Slice 3 — Project Context + Final Verification (Scenario 3)**
   Implement `IProjectContextService` and `ProjectContextService`. Register context retrieval as an additional tool in `PrioritizationService._chatOptions.Tools` via `AIFunctionFactory.Create()` so the agent can access it when generating suggestions. Update the agent's system prompt to reference context when available and note its absence when not. Create unit tests for `ProjectContextService` and verify context flows into agent responses. After all scenarios are implemented, run the full verification suite (build, all unit tests, all integration tests, `dotnet format`, manual smoke test) to confirm edge cases from Slices 1–2 are covered end-to-end.

---

## Global Test Strategy _(mandatory)_

### Unit Tests

| Scenario       | Test Project                | Test Class                       | Method Pattern                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| -------------- | --------------------------- | -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Scenario 1     | `app/MyOwnPo.App.UnitTests` | `BacklogServiceTests`            | `Connect_ValidLocation_ReturnsStories`, `Connect_EmptyBacklog_ReturnsEmptyList`, `Connect_SingleStory_ReturnsSingleStory`, `Connect_StoriesWithMissingFields_StoriesHaveMissingFieldsPopulated`, `GetStories_AfterConnect_ReturnsStoredStories`, `Refresh_StoriesAdded_ReportsAdded`, `Refresh_StoriesRemoved_ReportsRemoved`, `Refresh_StoriesChanged_ReportsChanged`, `Refresh_NoChanges_ReportsNoChanges`, `Connect_MoreThan100Stories_ReturnsLimitError`, `Connect_GatewayThrows_SurfacesGuidance` |
| Scenario 1     | `app/MyOwnPo.App.UnitTests` | `AzureDevOpsBacklogGatewayTests` | `ReadStories_ValidResponse_ReturnsAllStories`, `ReadStories_InvalidPat_ThrowsAuthException`, `ReadStories_ProjectNotFound_ThrowsDescriptiveException`, `ReadStories_EmptyBacklog_ReturnsEmptyList`, `ReadStories_WorkItemFieldMapping_MapsAllFieldsCorrectly`                                                                                                                                                                                                                                          |
| Scenario 2     | `app/MyOwnPo.App.UnitTests` | `PrioritizationServiceTests`     | `Chat_GeneralMessage_ReturnsAgentResponse`, `Chat_CallsGetResponseAsyncWithUserMessage`, `Chat_FirstMessage_IncludesSystemPrompt`, `Chat_MultipleCalls_AccumulatesHistory`, `Chat_NullResponseText_ReturnsEmptyString`, `Chat_ConfiguresChatOptionsWithTools`                                                                                                                                                                                                                                          |
| Scenario 3     | `app/MyOwnPo.App.UnitTests` | `ProjectContextServiceTests`     | `SetContext_ValidContext_StoresContext`, `UpdateContext_ExistingContext_ReplacesContext`, `GetContext_NoContextSet_ReturnsNull`                                                                                                                                                                                                                                                                                                                                                                        |
| Scenario 3     | `app/MyOwnPo.App.UnitTests` | `PrioritizationServiceTests`     | `Suggest_WithContext_JustificationsReferenceContext`, `Suggest_WithoutContext_NotesContextWouldHelp`                                                                                                                                                                                                                                                                                                                                                                                                   |
| Cross-scenario | `app/MyOwnPo.App.UnitTests` | `ConsoleHostTests`               | `HandleInput_ConnectCommand_CallsBacklogService`, `HandleInput_NaturalLanguage_ForwardsToAgent`, `HandleInput_NaturalLanguageWithNoBacklog_PromptsToConnect`, `HandleInput_ExitCommand_Exits`, `HandleInput_HelpCommand_ShowsHelp`, `HandleInput_RefreshCommand_CallsBacklogService`                                                                                                                                                                                                                   |

### Integration Tests

| Scenario   | Test Project                     | Test Class                         | Method Pattern                                                |
| ---------- | -------------------------------- | ---------------------------------- | ------------------------------------------------------------- |
| Scenario 1 | `tests/MyOwnPo.IntegrationTests` | `BacklogIngestionIntegrationTests` | `ConnectAndIngest_AzureDevOps_ReturnsExpectedSummary`         |
| Scenario 2 | `tests/MyOwnPo.IntegrationTests` | `PrioritizationIntegrationTests`   | `SuggestPriorities_IngestedBacklog_ReturnsCompleteRankedList` |

---

## Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnxx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj`
3. `dotnet test .\tests\MyOwnPo.IntegrationTests\MyOwnPo.IntegrationTests.csproj`
4. `dotnet format --verify-no-changes .\myownpo.slnxx`
5. Manual smoke test: run the console app, connect to an Azure DevOps backlog (using a real PAT and organization URL), request suggestions, verify output is well-formatted and suggestion-only.

---

## Instruction Compliance Checklist _(mandatory)_

- [ ] Console interaction layer (ConsoleHost) calls services, not gateways directly.
- [ ] Business orchestration (prioritization logic, context handling) remains in services, not gateways.
- [ ] DI wiring is defined in the composition root (`Program.cs`).
- [ ] Security concerns addressed: no secrets in code/logs; Azure DevOps PAT and Azure OpenAI API key loaded from user secrets only (never `appsettings.json`); other config loaded from `appsettings.json`; secrets never logged or displayed.
- [ ] Test additions align with project test expectations — unit tests for all services and gateways, integration tests for end-to-end flows.
- [ ] Style and naming adherence validated with `.editorconfig` and `dotnet format`.
- [ ] The system never modifies the actual backlog under any circumstances (FR-006, SC-004).
- [ ] Backlog cap of 100 stories is enforced with a clear user message.

---

## Unresolved Blockers _(mandatory)_

~~**LLM provider selection**~~: **Resolved** — Use Azure OpenAI via `Azure.AI.OpenAI` + `Microsoft.Extensions.AI.OpenAI`. Configuration: endpoint and deployment name supplied via `appsettings.json` in an `AzureOpenAi` section; API key supplied via user secrets (`AzureOpenAi:ApiKey`).

~~**Azure DevOps query scope**~~: **Resolved** — Connection config supplied via `appsettings.json` (`AzureDevOps:OrganizationUrl`, `AzureDevOps:ProjectName`, optional `AzureDevOps:AreaPath`). The backlog is hard-set to a specific project. The PAT is loaded from user secrets (`AzureDevOps:Pat`). The gateway uses a default WIQL query for all User Story work items in the configured project, excluding terminal states (Closed, Removed).

~~**Microsoft Agent Framework version**~~: **Resolved** — Use `Microsoft.Agents.AI.OpenAI` version **1.0.0-rc4** (bridges OpenAI SDK → `IChatClient`). `Microsoft.Agents.Builder` doesn't exist on NuGet in the expected form and targets M365/Teams bot routing. Agent behavior defined via system prompt + tools in `PrioritizationService` using `IChatClient` directly with `UseFunctionInvocation()` middleware. Also requires `Azure.AI.OpenAI` 2.3.0-beta.1 (not transitive).

`None` — all blockers resolved.

---

## Revisions

### Session 2026-03-24

- **C1** (Consistency / HIGH): Global Test Strategy test names mismatched scenario plans. → **Fix**: Updated all test names in master plan to match authoritative scenario plan names; expanded `BacklogServiceTests`, `AzureDevOpsBacklogGatewayTests`, `PrioritizationServiceTests`, and `ConsoleHostTests` to full scenario-plan lists.
- **U4** (Underspec / MEDIUM): Slice 4 "Edge Cases + Hardening" had no corresponding plan and duplicated work from Slices 1–3. → **Fix**: Removed Slice 4; added final verification note to Slice 3.
- **Blocker resolution**: LLM provider → Azure OpenAI (`Azure.AI.OpenAI` + `Microsoft.Extensions.AI.OpenAI`). Azure DevOps query scope → config via `appsettings.json`, PAT via user secrets, hard-set to specific backlog. Agent Framework version → `Microsoft.Agents.Builder` 1.0.0-rc4. All three blockers resolved; Dependencies, Cross-Scenario Tasks, security checklist, and Unresolved Blockers updated.

### Session 2026-03-24 #2

- **C1** (Consistency / HIGH): Integration test names drifted between master and scenario plans. → **Fix**: Aligned master integration method names to scenario plan canon (`ConnectAndIngest_AzureDevOps_ReturnsExpectedSummary`, `SuggestPriorities_IngestedBacklog_ReturnsCompleteRankedList`).

### Session 2026-03-27

- **Filtering**: WIQL query now excludes Closed and Removed work items. Updated Slice 1 and Unresolved Blockers to reflect `[System.State] NOT IN ('Closed', 'Removed')` filter.

### Session 2026-03-27 #2 — Scenario 2 Implementation

- **Framework change** (CRITICAL): `Microsoft.Agents.Builder` doesn't exist on NuGet; replaced with `Microsoft.Agents.AI.OpenAI` 1.0.0-rc4 + `Azure.AI.OpenAI` 2.3.0-beta.1. Updated Dependencies, Cross-Scenario Tasks, Delivery Sequence, and Unresolved Blockers.
- **Architecture**: No separate agent definition class. Agent behavior (system prompt, tools, conversation history) lives in `PrioritizationService` using `IChatClient` with `UseFunctionInvocation()` middleware and `AIFunctionFactory.Create()`.
- **Test alignment**: Updated `PrioritizationServiceTests` and `ConsoleHostTests` method names in Global Test Strategy to match actual implemented tests.
- **Slice 2 update**: Replaced "Agent Framework" references with `IChatClient`/`AIFunctionFactory`/`UseFunctionInvocation()`. Added `TextReader`/`TextWriter` testability note to `ConsoleHost`.
- **Slice 3 update**: Replaced agent definition reference with `PrioritizationService._chatOptions.Tools` + `AIFunctionFactory` pattern.
