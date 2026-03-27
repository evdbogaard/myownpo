# Scenario Plan: Connect to an Existing Backlog

**Created**: 2026-03-20
**Spec**: [specs/2026-03-20-ai-product-owner/spec.md](specs/2026-03-20-ai-product-owner/spec.md)
**Master Plan**: [specs/2026-03-20-ai-product-owner/plans/plan.md](specs/2026-03-20-ai-product-owner/plans/plan.md)
**Scenario Priority**: P1
**Scenario Index**: 1

---

## Scenario Goal _(mandatory)_

Enable a team member to point the AI Product Owner at an Azure DevOps backlog (configured via `appsettings.json` for organization URL and project; PAT provided via user secrets), ingest all user stories with their fields via the Azure DevOps REST API, and present a summary so the team member can verify correctness. Support refreshing the backlog to detect changes.

---

## Acceptance Traceability _(mandatory)_

- **Scenario Acceptance Criteria**:
  1. **Given** the team member provides the location of their existing backlog, **When** the AI Product Owner connects to it, **Then** it reads all user stories and presents a summary showing the total number of stories and their titles.
  2. **Given** a connected backlog, **When** some stories have incomplete information (e.g., missing descriptions or acceptance criteria), **Then** the AI Product Owner still ingests them and notes which stories have missing fields.
  3. **Given** a previously connected backlog, **When** the team member asks the AI Product Owner to refresh, **Then** it re-reads the backlog and identifies any stories that were added, removed, or changed since the last read.
- **Related Requirements**: FR-001, FR-002, FR-003, FR-010
- **Related Edge Cases**: Empty backlog, single story, inaccessible backlog, >100 stories

---

## Technical Context _(mandatory)_

- **Current Behavior**: `Program.cs` prints "Hello, World!" — no backlog functionality exists.
- **Target Behavior**: The console app reads the Azure DevOps organization URL and project name from `appsettings.json` and the PAT from user secrets. A `connect` command (or agent tool invocation) authenticates with Azure DevOps, queries work items of type "User Story" using WIQL, maps them to domain models, stores them in memory, and prints a summary (total count, titles, missing-field warnings). A `refresh` command re-queries Azure DevOps and reports differences (added, removed, changed stories).
- **Architecture Boundaries**:
  - `ConsoleHost` (controller-equivalent) dispatches commands to `IBacklogService`.
  - `BacklogService` orchestrates connection, ingestion, summary, and refresh logic.
  - `IBacklogGateway` / `AzureDevOpsBacklogGateway` handles Azure DevOps REST API calls and work item mapping.
- **Dependencies**: `Microsoft.TeamFoundation.WorkItemTracking.WebApi` + `Microsoft.VisualStudio.Services.Client` for Azure DevOps API, `Microsoft.Extensions.Configuration.UserSecrets` for PAT, `Microsoft.Extensions.DependencyInjection` for DI.

---

## Implementation Tasks _(mandatory)_

- [ ] **TASK-1-01**: Define `UserStory` model with properties: `Id`, `Title`, `Description`, `AcceptanceCriteria`, `Priority`, `Labels` (list), `Status`. Add computed property `MissingFields` that returns names of null/empty fields. See [models.md](specs/2026-03-20-ai-product-owner/plans/models.md#userstory) for full definition and Azure DevOps field mapping.
  - **Layer**: Models
  - **Reason**: Core domain entity needed by all scenarios. Must capture all fields mentioned in FR-002.

- [ ] **TASK-1-02**: Define `BacklogConnection` model with properties: `IsConnected` (bool), `LastRefreshed` (DateTimeOffset?), `StoryCount` (int). Provider-agnostic runtime state only. See [models.md](specs/2026-03-20-ai-product-owner/plans/models.md#backlogconnection) for full definition.
  - **Layer**: Models
  - **Reason**: Tracks runtime connection state. No provider-specific fields — gateway configuration lives in `AzureDevOpsSettings`.

- [ ] **TASK-1-02b**: Define `AzureDevOpsSettings` with properties: `OrganizationUrl` (string), `ProjectName` (string), `AreaPath` (string?), `Pat` (string). See [models.md](specs/2026-03-20-ai-product-owner/plans/models.md#azuredevopssettings) for full definition.
  - **Layer**: Gateways
  - **Reason**: Azure DevOps-specific config bound from `appsettings.json` + user secrets. Owned by the gateway, not a domain model.

- [ ] **TASK-1-04**: Define `BacklogDiff` model with properties: `Added` (list of string), `Removed` (list of string), `Changed` (list of string). See [models.md](specs/2026-03-20-ai-product-owner/plans/models.md#backlogdiff) for full definition.
  - **Layer**: Models
  - **Reason**: Structured return type for the refresh diff display (AC 3).

- [ ] **TASK-1-05**: Define `IBacklogGateway` interface with methods: `Task<IReadOnlyList<UserStory>> ReadStories()`.
  - **Layer**: Gateways
  - **Reason**: Abstraction to support multiple backlog sources. The gateway reads its own configuration via DI — no parameters needed. MVP implements Azure DevOps; future implementations could cover Jira, GitHub Issues, etc.

- [ ] **TASK-1-06**: Implement `AzureDevOpsBacklogGateway : IBacklogGateway`.
  - Inject `IOptions<AzureDevOpsSettings>` for configuration.
  - Authenticate using PAT via `VssBasicCredential`.
  - Execute a WIQL query to retrieve all work items of type "User Story" in the configured project.
  - Map Azure DevOps work item fields to `UserStory` domain model (Title → `System.Title`, Description → `System.Description`, Acceptance Criteria → `Microsoft.VSTS.Common.AcceptanceCriteria`, Priority → `Microsoft.VSTS.Common.Priority`, State → `System.State`, Tags → `System.Tags`).
  - Throw a descriptive exception if authentication fails (invalid/expired PAT), project is not found, or the API is unreachable.
  - **Layer**: Gateways
  - **Reason**: Azure DevOps backlog source. Data-access focused — no business logic.

- [ ] **TASK-1-07**: Define `IBacklogService` interface with methods: `Task<IReadOnlyList<UserStory>> Connect()`, `IReadOnlyList<UserStory> GetStories()`, `Task<BacklogDiff> Refresh()`.
  - **Layer**: Services
  - **Reason**: Business-layer abstraction for backlog operations. `Connect` returns the story list directly — consumers derive summaries and missing-field info from the stories themselves.

- [ ] **TASK-1-08**: Implement `BacklogService : IBacklogService`.
  - `Connect`: call gateway (no parameters — gateway has its own settings), validate story count (≤100 — throw `BacklogCapExceededException` with the actual count if exceeded), store stories in memory, update `BacklogConnection` state, return the story list.
  - `GetStories`: return current in-memory story list.
  - `Refresh`: re-read via gateway, diff against stored stories (by Id), update stored stories, return diff.
  - Handle edge cases: empty backlog (report zero stories), >100 stories (throw `BacklogCapExceededException`; `ConsoleHost` catches and displays guidance to narrow scope), inaccessible Azure DevOps (surface gateway exception with guidance on PAT and URL).
  - **Layer**: Services
  - **Reason**: Orchestrates ingestion, validation, and diffing. Single-story edge case handled here too.

- [ ] **TASK-1-09**: Create `ConsoleHost` class with an interactive command loop.
  - Parse `connect` → call `IBacklogService.Connect`, display story count, titles, and any missing-field warnings from the returned story list.
  - Parse `refresh` → call `IBacklogService.Refresh`, display diff.
  - Parse `help` → display available commands.
  - Parse `exit` / `quit` → terminate.
  - Unknown commands → display help text.
  - **Layer**: Console Interaction (Controller-equivalent)
  - **Reason**: Entry point for user interaction. Must only call services, never gateways.

- [ ] **TASK-1-10**: Update `Program.cs` to set up DI container, configure user secrets and `appsettings.json`, register services and gateways, and run `ConsoleHost`.
  - **Layer**: Composition Root
  - **Reason**: Wires everything together. DI registration and configuration must happen here.

- [ ] **TASK-1-11**: Create `appsettings.json` with Azure DevOps configuration section (`AzureDevOps:OrganizationUrl`, `AzureDevOps:ProjectName`, optional `AzureDevOps:AreaPath`). Bind to `AzureDevOpsSettings` via `services.Configure<AzureDevOpsSettings>()`. The PAT must be set via user secrets: `dotnet user-secrets set "AzureDevOps:Pat" "<value>"`. Initialize user secrets with `dotnet user-secrets init`.
  - **Layer**: Configuration
  - **Reason**: Separates connection config (appsettings) from secrets (user secrets). PAT must never be in source control.

- [ ] **TASK-1-12**: Create unit test project `app/MyOwnPo.App.UnitTests/MyOwnPo.App.UnitTests.csproj` with xUnit and a mocking library.
  - **Layer**: Test Infrastructure
  - **Reason**: Foundation for all unit tests across scenarios.

---

## Files To Alter _(mandatory)_

| File                                                        | Change Type | Why                                                                         |
| ----------------------------------------------------------- | ----------- | --------------------------------------------------------------------------- |
| `Models/UserStory.cs`                                       | Add         | Core domain model for backlog stories                                       |
| `Models/BacklogConnection.cs`                               | Add         | Provider-agnostic runtime connection state                                  |
| `Models/BacklogDiff.cs`                                     | Add         | Diff return type for refresh                                                |
| `Gateways/IBacklogGateway.cs`                               | Add         | Abstraction for backlog data access                                         |
| `Gateways/AzureDevOpsSettings.cs`                           | Add         | Azure DevOps-specific configuration (bound from appsettings + user secrets) |
| `Gateways/AzureDevOpsBacklogGateway.cs`                     | Add         | Azure DevOps REST API backlog reader                                        |
| `Services/IBacklogService.cs`                               | Add         | Service interface for backlog operations                                    |
| `Services/BacklogService.cs`                                | Add         | Service implementation with business logic                                  |
| `Services/BacklogCapExceededException.cs`                   | Add         | Explicit exception type used when backlog size exceeds 100 stories          |
| `ConsoleHost.cs`                                            | Add         | Interactive console command loop                                            |
| `Program.cs`                                                | Modify      | Set up DI, config, user secrets, and run ConsoleHost                        |
| `app/MyOwnPo.App/MyOwnPo.App.csproj`                                            | Modify      | Add NuGet package references (Hosting, Azure DevOps client, UserSecrets)    |
| `appsettings.json`                                          | Add         | Azure DevOps organization URL and project config                            |
| `app/MyOwnPo.App.UnitTests/MyOwnPo.App.UnitTests.csproj`          | Add         | Unit test project                                                           |
| `app/MyOwnPo.App.UnitTests/BacklogServiceTests.cs`            | Add         | Unit tests for BacklogService                                               |
| `app/MyOwnPo.App.UnitTests/AzureDevOpsBacklogGatewayTests.cs` | Add         | Unit tests for AzureDevOpsBacklogGateway                                    |

---

## Technical Questions _(mandatory)_

- **Azure DevOps work item field mapping**: Confirm the exact field reference names for all required fields. Standard mapping assumed: `System.Title`, `System.Description`, `Microsoft.VSTS.Common.AcceptanceCriteria`, `Microsoft.VSTS.Common.Priority`, `System.State`, `System.Tags`. Custom fields may vary by organization.
- **Story identity for diffing**: Azure DevOps work items have an integer `Id`. Use this as the primary key for refresh diffing (`UserStory.Id` will be `string` to keep the gateway abstraction flexible, storing the Azure DevOps Id as a string).
- **WIQL query scope**: Resolved — include all User Story work items in the configured project by default; optional narrowing uses `AreaPath` from `appsettings.json`.
- **PAT permissions**: Document the minimum required PAT scope: `Work Items (Read)` at the project level.

---

## Testing Criteria _(mandatory)_

### Unit Tests

- **Type**: Unit
- **Project**: `app/MyOwnPo.App.UnitTests`
- **Class**: `BacklogServiceTests`
- **Methods to add/update**:
  - `Connect_ValidLocation_ReturnsStories`
  - `Connect_EmptyBacklog_ReturnsEmptyList`
  - `Connect_SingleStory_ReturnsSingleStory`
  - `Connect_MoreThan100Stories_ReturnsLimitError`
  - `Connect_GatewayThrows_SurfacesGuidance`
  - `Connect_StoriesWithMissingFields_StoriesHaveMissingFieldsPopulated`
  - `GetStories_AfterConnect_ReturnsStoredStories`
  - `Refresh_StoriesAdded_ReportsAdded`
  - `Refresh_StoriesRemoved_ReportsRemoved`
  - `Refresh_StoriesChanged_ReportsChanged`
  - `Refresh_NoChanges_ReportsNoChanges`

- **Class**: `AzureDevOpsBacklogGatewayTests`
- **Methods to add/update**:
  - `ReadStories_ValidResponse_ReturnsAllStories`
  - `ReadStories_InvalidPat_ThrowsAuthException`
  - `ReadStories_ProjectNotFound_ThrowsDescriptiveException`
  - `ReadStories_EmptyBacklog_ReturnsEmptyList`
  - `ReadStories_WorkItemFieldMapping_MapsAllFieldsCorrectly`

### Integration Tests

- **Type**: Integration
- **Required**: Yes — end-to-end from Azure DevOps API to summary verifies the REST client + field mapping + service logic working together. Requires a real or emulated Azure DevOps instance.
- **Project**: `tests/MyOwnPo.IntegrationTests`
- **Class**: `BacklogIngestionIntegrationTests`
- **Methods to add/update**:
  - `ConnectAndIngest_AzureDevOps_ReturnsExpectedSummary`
  - `ConnectAndRefresh_AzureDevOps_ReportsCorrectDiff`

---

## Scenario Verification Steps _(mandatory)_

1. `dotnet build .\myownpo.slnxx`
2. `dotnet test .\app\MyOwnPo.App.UnitTests\MyOwnPo.App.UnitTests.csproj --filter "FullyQualifiedName~BacklogService|FullyQualifiedName~AzureDevOpsBacklogGateway"`
3. `dotnet test .\tests\MyOwnPo.IntegrationTests\MyOwnPo.IntegrationTests.csproj --filter "FullyQualifiedName~BacklogIngestion"` (requires Azure DevOps PAT in user secrets)
4. `dotnet format --verify-no-changes .\myownpo.slnxx`
5. Manual smoke test: run the app, type `connect`, verify it connects to Azure DevOps and displays a summary of stories.

---

## Scenario Compliance Checks _(mandatory)_

- [ ] No direct gateway calls from `ConsoleHost` — all go through `IBacklogService`.
- [ ] Service boundary respected: `BacklogService` orchestrates ingestion logic, `AzureDevOpsBacklogGateway` only calls Azure DevOps APIs.
- [ ] Security checks: PAT is loaded from user secrets only, never from `appsettings.json` or source control; PAT is never logged or displayed; HTTPS enforced for Azure DevOps API calls.
- [ ] Tests and naming conventions aligned with project standards (xUnit, `[Subject]Tests`, `[Method]_[Condition]_[Expected]`).
- [ ] > 100 story cap enforced with clear user message (edge case from spec).

---

## Revisions

### Session 2026-03-24

- **U2** (Underspec / HIGH): TASK-1-08 `Connect` lacked error mechanism for >100 stories. → **Fix**: Specified `BacklogCapExceededException` as the thrown exception with actual count; `ConsoleHost` catches and displays guidance.
- **D1** (Duplication / MEDIUM): TASK-1-08 had duplicate `GetStories` line. → **Fix**: Removed duplicate line (resolved as part of U2 edit).
- **Blocker resolution**: PAT stays in user secrets; other Azure DevOps config (org URL, project, area path) via `appsettings.json`. Updated TASK-1-10, TASK-1-11, scenario goal, technical context, files table, verification steps, and compliance checks.

### Session 2026-03-24 #2

- **I1** (Instruction / CRITICAL): Scenario task layer labels used custom taxonomy. → **Fix**: Added explicit Layer Label Mapping that maps existing labels to template-required taxonomy.
- **C2** (Consistency / HIGH): Scenario-1 Technical Questions still treated WIQL scope as unresolved while master marked it resolved. → **Fix**: Replaced open question with resolved decision text aligned to master plan.
- **U2** (Underspec / MEDIUM): `BacklogCapExceededException` required by tasks but missing from Files To Alter. → **Fix**: Added `Services/BacklogCapExceededException.cs` to Files To Alter.
