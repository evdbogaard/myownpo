# Domain Models

**Created**: 2026-03-20
**Spec**: [specs/2026-03-20-ai-product-owner/spec.md](specs/2026-03-20-ai-product-owner/spec.md)
**Master Plan**: [specs/2026-03-20-ai-product-owner/plans/plan.md](specs/2026-03-20-ai-product-owner/plans/plan.md)

All models live in the `Models/` folder. Nullable reference types are enabled project-wide. Immutable models use `record` for value equality and concise syntax. Mutable models use `class`. No base classes, no ORM annotations, no business logic (except computed properties noted below).

---

## UserStory

**File**: `Models/UserStory.cs`
**Introduced in**: Scenario 1 (TASK-1-01)
**Used by**: All scenarios

The core domain entity representing a work item read from the external backlog. Maps 1:1 to an Azure DevOps User Story work item.

```csharp
public record UserStory
{
	public required string Id { get; init; }
	public required string Title { get; init; }
	public string? Description { get; init; }
	public string? AcceptanceCriteria { get; init; }
	public int? Priority { get; init; }
	public IReadOnlyList<string> Labels { get; init; } = [];
	public string? Status { get; init; }

	public IReadOnlyList<string> MissingFields =>
		GetMissingFields().ToList();

	private IEnumerable<string> GetMissingFields()
	{
		if (string.IsNullOrWhiteSpace(Description))
			yield return nameof(Description);

		if (string.IsNullOrWhiteSpace(AcceptanceCriteria))
			yield return nameof(AcceptanceCriteria);

		if (Priority is null)
			yield return nameof(Priority);

		if (string.IsNullOrWhiteSpace(Status))
			yield return nameof(Status);
	}
}
```

**Field mapping from Azure DevOps**:

| Property             | Azure DevOps Field Reference                   | Notes                                             |
| -------------------- | ----------------------------------------------- | ------------------------------------------------- |
| `Id`                 | `System.Id`                                     | Integer in Azure DevOps, stored as `string` here for gateway abstraction flexibility |
| `Title`              | `System.Title`                                  | Required — always present in Azure DevOps         |
| `Description`        | `System.Description`                            | HTML content; strip tags or store raw TBD         |
| `AcceptanceCriteria` | `Microsoft.VSTS.Common.AcceptanceCriteria`      | HTML content; may be empty                        |
| `Priority`           | `Microsoft.VSTS.Common.Priority`                | Integer 1–4 in Azure DevOps; nullable if unset    |
| `Labels`             | `System.Tags`                                   | Semicolon-delimited string in Azure DevOps; split into list |
| `Status`             | `System.State`                                  | e.g., "New", "Active", "Resolved", "Closed"      |

**Design decisions**:
- `Id` is `string` (not `int`) so the `IBacklogGateway` abstraction stays provider-agnostic. Azure DevOps uses integer IDs; other providers may use GUIDs or slugs.
- `required` + `init` on `Id` and `Title` because a story without these is invalid regardless of source.
- `MissingFields` is a computed read-only property — not serialized, not stored, derived on access. Used by the console display layer to report incomplete stories. Note: this prevents using a positional `record` constructor, which is fine — the property-based record syntax is clearer for this many fields.
- `Labels` defaults to an empty list rather than null to avoid null-check noise in consumers.

---

## BacklogConnection

**File**: `Models/BacklogConnection.cs`
**Introduced in**: Scenario 1 (TASK-1-02)
**Used by**: Scenario 1

Provider-agnostic runtime state of the current backlog connection. Contains no provider-specific configuration — that lives in the gateway’s own settings class (e.g., `AzureDevOpsSettings`). Created and managed by `BacklogService`.

```csharp
public class BacklogConnection
{
	public bool IsConnected { get; set; }
	public DateTimeOffset? LastRefreshed { get; set; }
	public int StoryCount { get; set; }
}
```

**Design decisions**:
- All properties are mutable — they change after connect and refresh operations.
- No provider-specific fields. The gateway reads its own configuration (URL, project, PAT) via DI-injected options, not from this model.
- `IBacklogGateway.ReadStories()` takes no parameters — the gateway already has its settings. `BacklogConnection` is purely for the service layer to track runtime state.

---

## AzureDevOpsSettings

**File**: `Gateways/AzureDevOpsSettings.cs`
**Introduced in**: Scenario 1 (TASK-1-02b)
**Used by**: Scenario 1 (gateway only)

Azure DevOps-specific configuration bound from the `AzureDevOps` section in `appsettings.json` and user secrets. Lives in `Gateways/` because it's owned by the gateway — not a domain model.

```csharp
public class AzureDevOpsSettings
{
	public required string OrganizationUrl { get; init; }
	public required string ProjectName { get; init; }
	public string? AreaPath { get; init; }
	public required string Pat { get; init; }
}
```

**Design decisions**:
- `Pat` is included here (loaded from user secrets via `IOptions<AzureDevOpsSettings>`) so the gateway has everything it needs in one place. The composition root binds both `appsettings.json` and user secrets into this class.
- `required init` on `OrganizationUrl`, `ProjectName`, and `Pat` — the gateway cannot function without these.
- `AreaPath` is optional — allows narrowing the WIQL query scope.
- This class is registered via `services.Configure<AzureDevOpsSettings>(configuration.GetSection("AzureDevOps"))` in `Program.cs`.

---

## BacklogDiff

**File**: `Models/BacklogDiff.cs`
**Introduced in**: Scenario 1 (TASK-1-04)
**Used by**: Scenario 1

Returned by `BacklogService.Refresh()`. Reports what changed since the last ingestion.

```csharp
public record BacklogDiff
{
	public required IReadOnlyList<string> Added { get; init; }
	public required IReadOnlyList<string> Removed { get; init; }
	public required IReadOnlyList<string> Changed { get; init; }

	public bool HasChanges => Added.Count > 0 || Removed.Count > 0 || Changed.Count > 0;
}
```

**Design decisions**:
- Lists contain story titles (not IDs) for human-readable display.
- `HasChanges` is a convenience computed property for the console display layer.
- "Changed" means any field on the `UserStory` differs. The comparison strategy (field-by-field hash or value equality) is an implementation detail of `BacklogService`, not this model.

---

## SuggestedStoryRank

**File**: `Models/SuggestedStoryRank.cs`
**Introduced in**: Scenario 2 (TASK-2-01)
**Used by**: Scenario 2, Scenario 3

Represents one story's position in a prioritization suggestion.

```csharp
public record SuggestedStoryRank
{
	public required string StoryId { get; init; }
	public required string StoryTitle { get; init; }
	public required int Rank { get; init; }
	public required string Justification { get; init; }
}
```

**Design decisions**:
- Includes both `StoryId` and `StoryTitle` so the display layer doesn't need to look up titles separately.
- `Rank` is 1-based (1 = highest priority).
- `Justification` is the agent's natural-language explanation for why this story is at this position.

---

## PrioritizationSuggestion

**File**: `Models/PrioritizationSuggestion.cs`
**Introduced in**: Scenario 2 (TASK-2-02)
**Used by**: Scenario 2, Scenario 3

The complete result of a prioritization request. Wraps the ranked list and metadata.

```csharp
public record PrioritizationSuggestion
{
	public required IReadOnlyList<SuggestedStoryRank> Rankings { get; init; }
	public required DateTimeOffset Created { get; init; }
	public IReadOnlyList<string> DuplicateWarnings { get; init; } = [];
}
```

**Design decisions**:
- `Rankings` is ordered by `Rank` (index 0 = rank 1).
- `DuplicateWarnings` is a list of human-readable warning strings (e.g., "Stories 'Add login' and 'Add user login' may be duplicates"). Defaults to empty — most backlogs won't have duplicates.
- `Created` records when the suggestion was produced, useful if the team member asks for another suggestion later and wants to compare.

---

## ProjectContext

**File**: `Models/ProjectContext.cs`
**Introduced in**: Scenario 3 (TASK-3-01)
**Used by**: Scenario 3

Optional background information provided by the team member to improve suggestion quality. All fields are nullable — the team member can provide as much or as little as they want.

```csharp
public class ProjectContext
{
	public string? Vision { get; set; }
	public string? BusinessGoals { get; set; }
	public string? TargetUsers { get; set; }
	public string? SprintFocus { get; set; }
	public string? Constraints { get; set; }

	public bool IsEmpty =>
		string.IsNullOrWhiteSpace(Vision)
		&& string.IsNullOrWhiteSpace(BusinessGoals)
		&& string.IsNullOrWhiteSpace(TargetUsers)
		&& string.IsNullOrWhiteSpace(SprintFocus)
		&& string.IsNullOrWhiteSpace(Constraints);
}
```

**Design decisions**:
- All properties are `set` (not `init`) because context is mutable — the team member can update individual fields via `context update`.
- `IsEmpty` convenience property lets the agent tool and display layer quickly check if any context has been provided.
- No `required` properties — an empty `ProjectContext` is valid (the team member might set only one field at a time).

---

## AzureOpenAiSettings

**File**: `Gateways/AzureOpenAiSettings.cs`
**Introduced in**: Scenario 2 (TASK-2-07)
**Used by**: Scenario 2 (agent configuration)

Azure OpenAI-specific configuration bound from the `AzureOpenAi` section in `appsettings.json`. Lives in `Gateways/` alongside `AzureDevOpsSettings` as provider-specific config.

```csharp
public class AzureOpenAiSettings
{
	public required string Endpoint { get; init; }
	public required string DeploymentName { get; init; }
	public required string ApiKey { get; init; }
}
```

**Design decisions**:
- All three fields are `required` — the agent cannot function without an endpoint, deployment, and key.
- `Endpoint` and `DeploymentName` loaded from `appsettings.json` via `services.Configure<AzureOpenAiSettings>(configuration.GetSection("AzureOpenAi"))` in `Program.cs`. `ApiKey` loaded from user secrets (`AzureOpenAi:ApiKey`).
- Used to construct the `AzureOpenAIClient` and register `IChatClient` in the DI container.
