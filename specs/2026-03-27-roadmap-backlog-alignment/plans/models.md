# Models: Roadmap-Backlog Alignment and Context Guidance

**Created**: 2026-03-27
**Spec**: [specs/2026-03-27-roadmap-backlog-alignment/spec.md](specs/2026-03-27-roadmap-backlog-alignment/spec.md)
**Feature Plan**: [specs/2026-03-27-roadmap-backlog-alignment/plans/plan.md](specs/2026-03-27-roadmap-backlog-alignment/plans/plan.md)

---

## Purpose

This document defines the models to be created for roadmap/backlog alignment, where each model lives, and which scenario(s) use it.

---

## Model Definitions

### RoadmapItem

- **File**: `app/MyOwnPo.App/Models/RoadmapItem.cs`
- **Used by**: Scenario 1, Scenario 2, Scenario 3
- **Purpose**: Canonical representation of a roadmap initiative parsed from markdown.
- **Suggested shape**:
  - `string Id`
  - `string Title`
  - `string? Description`
  - `string? TimeHorizon`
  - `IReadOnlyList<string> Tags`

```csharp
namespace MyOwnPo.Models;

public record RoadmapItem
{
	public required string Id { get; init; }
	public required string Title { get; init; }
	public string? Description { get; init; }
	public string? TimeHorizon { get; init; }
	public IReadOnlyList<string> Tags { get; init; } = [];
}
```

### RoadmapStoryLinkRecommendation

- **File**: `app/MyOwnPo.App/Models/RoadmapStoryLinkRecommendation.cs`
- **Used by**: Scenario 1
- **Purpose**: One recommended relationship between a roadmap item and one backlog story.
- **Suggested shape**:
  - `string RoadmapItemId`
  - `string StoryId`
  - `string StoryTitle`
  - `string Rationale`
  - `string Confidence`

```csharp
namespace MyOwnPo.Models;

public record RoadmapStoryLinkRecommendation
{
	public required string RoadmapItemId { get; init; }
	public required string StoryId { get; init; }
	public required string StoryTitle { get; init; }
	public required string Rationale { get; init; }
	public required int ConfidencePercent { get; init; }
}
```

### RoadmapGapSuggestion

- **File**: `app/MyOwnPo.App/Models/RoadmapGapSuggestion.cs`
- **Used by**: Scenario 2
- **Purpose**: Suggested story titles for an uncovered roadmap item.
- **Suggested shape**:
  - `string RoadmapItemId`
  - `string RoadmapItemTitle`
  - `IReadOnlyList<string> SuggestedStoryTitles`
  - `string GuidanceSource`

```csharp
namespace MyOwnPo.Models;

public record RoadmapGapSuggestion
{
  public required string RoadmapItemId { get; init; }
  public required string RoadmapItemTitle { get; init; }
  public IReadOnlyList<string> SuggestedStoryTitles { get; init; } = [];
  public required string GuidanceSource { get; init; }
  public string? Error { get; init; }
}
```

### ProjectContextSuggestion

- **File**: `app/MyOwnPo.App/Models/ProjectContextSuggestion.cs`
- **Used by**: Scenario 3
- **Purpose**: Strategy summary generated from roadmap themes.
- **Suggested shape**:
  - `string Vision`
  - `IReadOnlyList<string> Goals` (up to 3)
  - `string? Warning`

```csharp
namespace MyOwnPo.Models;

public record ProjectContextSuggestion
{
	public required string Vision { get; init; }
	public IReadOnlyList<string> Goals { get; init; } = [];
	public string? Warning { get; init; }
}
```

### RoadmapAnalysisResult

- **File**: `app/MyOwnPo.App/Models/RoadmapAnalysisResult.cs`
- **Used by**: Scenario 1, Scenario 2, Scenario 3
- **Purpose**: Top-level analysis output produced in `PrioritizationService` tool-calling flow and returned through chat responses.
- **Suggested shape**:
  - `IReadOnlyList<RoadmapStoryLinkRecommendation> LinkedStories`
  - `IReadOnlyList<RoadmapItem> UnlinkedRoadmapItems`
  - `IReadOnlyList<RoadmapGapSuggestion> GapSuggestions`
  - `ProjectContextSuggestion? ContextSuggestion`

```csharp
namespace MyOwnPo.Models;

public record RoadmapAnalysisResult
{
	public IReadOnlyList<RoadmapStoryLinkRecommendation> LinkedStories { get; init; } = [];
	public IReadOnlyList<RoadmapItem> UnlinkedRoadmapItems { get; init; } = [];
	public IReadOnlyList<RoadmapGapSuggestion> GapSuggestions { get; init; } = [];
	public ProjectContextSuggestion? ContextSuggestion { get; init; }
}
```

### LoadedRoadmapState

- **File**: `app/MyOwnPo.App/Models/LoadedRoadmapState.cs`
- **Used by**: Scenario 1, Scenario 2, Scenario 3
- **Purpose**: In-memory roadmap session state retained by `PrioritizationService` for follow-up conversational decisions.
- **Suggested shape**:
  - `string SourcePath`
  - `DateTimeOffset LoadedAt`
  - `IReadOnlyList<RoadmapItem> Items`

```csharp
namespace MyOwnPo.Models;

public record LoadedRoadmapState
{
	public required string SourcePath { get; init; }
	public required DateTimeOffset LoadedAt { get; init; }
	public IReadOnlyList<RoadmapItem> Items { get; init; } = [];
}
```

---

## Non-Model Supporting Types

### MicrosoftLearnMcpSettings

- **File**: `app/MyOwnPo.App/Gateways/MicrosoftLearnMcpSettings.cs`
- **Purpose**: Configuration object for Microsoft Learn MCP connectivity.

```csharp
namespace MyOwnPo.Gateways;

public record MicrosoftLearnMcpSettings
{
  public required string ServerName { get; init; }
  public required string ToolsetName { get; init; }
  public string? Endpoint { get; init; }
}
```

---

## Relationship Notes

- `RoadmapAnalysisResult` composes outputs from all scenarios.
- `LoadedRoadmapState` is the shared in-memory context used between chat turns after roadmap load.
- `RoadmapStoryLinkRecommendation` enforces one story mapped to at most one roadmap item via service logic.
- `RoadmapGapSuggestion.SuggestedStoryTitles` is constrained to 1-5 entries by service validation.
- `ProjectContextSuggestion` returns one vision and up to three goals; if fewer than three goals can be derived, include a warning.
