using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.AI;

using MyOwnPo.Models;

namespace MyOwnPo.Services;

public class PrioritizationService : IPrioritizationService
{
	private const int MaxHistoryMessages = 20;
	private const string DefaultRoadmapPath = "roadmap.md";

	private static readonly string SystemPrompt = """
		You are the AI Product Owner — an expert in agile product management, user story prioritization, and backlog grooming.

		## Role
		You help the team by analyzing their backlog and suggesting a priority order for their user stories. You consider business value, dependencies, story completeness, and any context the team member shares.

		## Rules
		- You operate in **suggestion-only mode**. You NEVER modify, create, or delete stories in the backlog. If asked to change the backlog, politely decline and offer a suggestion instead.
		- When the team member asks for prioritization suggestions, call the GetBacklogStories tool to retrieve the current backlog, then rank ALL stories from highest to lowest priority.
		- For each story in your ranking, provide a clear justification explaining why it belongs at that position.
		- If the backlog has fewer than 2 stories, explain that meaningful prioritization requires at least 2 stories.
		- If the backlog is empty, tell the team member to connect to a backlog first using the 'connect' command.
		- When you detect stories with very similar titles or descriptions, flag them as potential duplicates in your response.
		- When the team member asks follow-up questions (e.g., "why is story X above story Y?"), explain your reasoning using the context from the conversation.
		- When the team member provides feedback or additional context, incorporate it and offer an updated ranking if appropriate.
		- Keep responses clear, well-formatted, and actionable. Use numbered lists for rankings.
		- The story existing only out of just dashes (----), this is a separator story that shows stories above it are ready to be picked up or active, below it are stories that need more information or refinement.
		- When generating prioritization suggestions, always check for project context using the GetProjectContext tool. If context exists, reference the vision, goals, and constraints in your justifications. If no context is set, note that providing context would improve the quality of suggestions.
		- When the user asks for roadmap analysis, first call LoadRoadmap with the default path unless they provide a specific path.
		- For roadmap analysis, call EvaluateRoadmapStoryLinks after loading the roadmap and present results in two sections: linked roadmap items and unlinked roadmap items.
		- Roadmap linking only considers stories with Status set to New.
		- A backlog story can only be linked to one roadmap item. Never reuse a story in a second roadmap link.
		- Include a concise business rationale and confidence percentage (0-100) for each roadmap link.
		""";

	private readonly IChatClient _chatClient;
	private readonly IBacklogService _backlogService;
	private readonly IProjectContextService _projectContextService;
	private readonly IRoadmapFileLoader _roadmapFileLoader;
	private readonly IRoadmapParser _roadmapParser;
	private readonly List<ChatMessage> _history = [];
	private readonly ChatOptions _chatOptions;
	private LoadedRoadmapState? _loadedRoadmap;

	public PrioritizationService(
		IChatClient chatClient,
		IBacklogService backlogService,
		IProjectContextService projectContextService,
		IRoadmapFileLoader roadmapFileLoader,
		IRoadmapParser roadmapParser)
	{
		_chatClient = chatClient;
		_backlogService = backlogService;
		_projectContextService = projectContextService;
		_roadmapFileLoader = roadmapFileLoader;
		_roadmapParser = roadmapParser;

		_history.Add(new ChatMessage(ChatRole.System, SystemPrompt));

		_chatOptions = new ChatOptions
		{
			Tools =
			[
				AIFunctionFactory.Create(GetBacklogStories, "GetBacklogStories", "Retrieves all user stories currently loaded from the backlog."),
				AIFunctionFactory.Create(GetProjectContext, "GetProjectContext", "Retrieves the project context (vision, goals, target users, sprint focus, constraints) if set by the team member."),
				AIFunctionFactory.Create(LoadRoadmap, "LoadRoadmap", "Loads roadmap markdown items from disk. Uses roadmap.md when no file path is provided."),
				AIFunctionFactory.Create(EvaluateRoadmapStoryLinks, "EvaluateRoadmapStoryLinks", "Evaluates links between loaded roadmap items and New-state backlog stories, including rationale and confidence.")
			]
		};
	}

	public async Task<string> Chat(string userMessage)
	{
		_history.Add(new ChatMessage(ChatRole.User, userMessage));
		TrimHistory();

		var response = await _chatClient.GetResponseAsync(_history, _chatOptions);
		_history.AddMessages(response);

		return response.Text ?? string.Empty;
	}

	[Description("Retrieves the project context (vision, goals, target users, sprint focus, constraints) if set by the team member.")]
	private string GetProjectContext()
	{
		var context = _projectContextService.GetContext();
		if (context is null || context.IsEmpty)
			return "No project context has been set. Suggest that providing context (vision, goals, target users, sprint focus, constraints) would improve prioritization quality.";

		return JsonSerializer.Serialize(new
		{
			context.Vision,
			context.BusinessGoals,
			context.TargetUsers,
			context.SprintFocus,
			context.Constraints
		});
	}

	[Description("Retrieves all user stories currently loaded from the backlog.")]
	private string GetBacklogStories()
	{
		var stories = _backlogService.GetStories();
		return JsonSerializer.Serialize(stories.Select(story => new
		{
			story.Id,
			story.Title,
			story.Description,
			story.AcceptanceCriteria,
			story.Priority,
			story.Labels,
			story.Status,
			story.MissingFields
		}));
	}

	[Description("Loads roadmap markdown items from disk. Uses roadmap.md when no file path is provided.")]
	private string LoadRoadmap(string? filePath = null)
	{
		var effectivePath = string.IsNullOrWhiteSpace(filePath)
			? DefaultRoadmapPath
			: filePath.Trim();

		try
		{
			var markdown = _roadmapFileLoader.Load(effectivePath);
			var items = _roadmapParser.Parse(markdown);

			_loadedRoadmap = new LoadedRoadmapState
			{
				SourcePath = effectivePath,
				LoadedAt = DateTimeOffset.UtcNow,
				Items = items
			};

			return JsonSerializer.Serialize(new
			{
				Path = effectivePath,
				Count = items.Count,
				LoadedAt = _loadedRoadmap.LoadedAt,
				Items = items.Select(item => new
				{
					item.Id,
					item.Title,
					item.Description,
					item.TimeHorizon,
					item.Tags
				})
			});
		}
		catch (Exception ex)
		{
			return JsonSerializer.Serialize(new
			{
				Path = effectivePath,
				Error = ex.Message
			});
		}
	}

	[Description("Evaluates links between loaded roadmap items and New-state backlog stories, including rationale and confidence.")]
	private async Task<string> EvaluateRoadmapStoryLinks()
	{
		if (_loadedRoadmap is null)
		{
			return JsonSerializer.Serialize(new
			{
				Error = "No roadmap is loaded. Call LoadRoadmap first."
			});
		}

		var newStories = _backlogService
			.GetStories()
			.Where(story => string.Equals(story.Status, "New", StringComparison.OrdinalIgnoreCase))
			.ToList();

		var result = new RoadmapAnalysisResult();
		if (_loadedRoadmap.Items.Count == 0)
		{
			return JsonSerializer.Serialize(result);
		}

		if (newStories.Count == 0)
		{
			result = result with
			{
				UnlinkedRoadmapItems = _loadedRoadmap.Items
			};

			return JsonSerializer.Serialize(result);
		}

		var availableStories = newStories.ToDictionary(story => story.Id, StringComparer.OrdinalIgnoreCase);
		var linked = new List<RoadmapStoryLinkRecommendation>();
		var unlinked = new List<RoadmapItem>();

		foreach (var roadmapItem in _loadedRoadmap.Items)
		{
			var candidate = availableStories.Values
				.Select(story => new
				{
					Story = story,
					Score = CalculateMatchScore(roadmapItem, story)
				})
				.Where(candidate => candidate.Score > 0)
				.OrderByDescending(candidate => candidate.Score)
				.ThenBy(candidate => candidate.Story.Title, StringComparer.OrdinalIgnoreCase)
				.FirstOrDefault();

			if (candidate is null)
			{
				unlinked.Add(roadmapItem);
				continue;
			}

			availableStories.Remove(candidate.Story.Id);

			var explanation = await GenerateBusinessRationaleAsync(roadmapItem, candidate.Story, candidate.Score);
			linked.Add(new RoadmapStoryLinkRecommendation
			{
				RoadmapItemId = roadmapItem.Id,
				StoryId = candidate.Story.Id,
				StoryTitle = candidate.Story.Title,
				Rationale = explanation.Rationale,
				ConfidencePercent = explanation.ConfidencePercent
			});
		}

		result = result with
		{
			LinkedStories = linked,
			UnlinkedRoadmapItems = unlinked
		};

		return JsonSerializer.Serialize(result);
	}

	private async Task<LinkExplanation> GenerateBusinessRationaleAsync(RoadmapItem roadmapItem, UserStory story, int score)
	{
		var prompt = $$"""
			Write a concise business rationale for linking the roadmap item to the backlog story.
			Return strict JSON with this exact shape: {\"rationale\":\"...\",\"confidencePercent\":0}
			Confidence must be an integer from 0 to 100.

			Roadmap item:
			- Title: {{roadmapItem.Title}}
			- Description: {{roadmapItem.Description ?? "(none)"}}

			Backlog story:
			- Title: {{story.Title}}
			- Description: {{story.Description ?? "(none)"}}

			Deterministic overlap score (0-100): {{score}}
		""";

		var response = await _chatClient.GetResponseAsync(
			[
				new ChatMessage(ChatRole.System, "You produce compact JSON only."),
				new ChatMessage(ChatRole.User, prompt)
			],
			new ChatOptions());

		var text = response.Text;
		if (!string.IsNullOrWhiteSpace(text))
		{
			try
			{
				using var document = JsonDocument.Parse(text);
				var root = document.RootElement;
				var rationale = root.TryGetProperty("rationale", out var rationaleElement)
					? rationaleElement.GetString()
					: null;
				var confidence = root.TryGetProperty("confidencePercent", out var confidenceElement)
					? confidenceElement.GetInt32()
					: score;

				if (!string.IsNullOrWhiteSpace(rationale))
				{
					return new LinkExplanation
					{
						Rationale = rationale,
						ConfidencePercent = ClampConfidence(confidence)
					};
				}
			}
			catch (JsonException)
			{
				// Fallback below when response is not strict JSON.
			}
		}

		return new LinkExplanation
		{
			Rationale = $"This story supports '{roadmapItem.Title}' because it directly addresses a similar user outcome and implementation theme.",
			ConfidencePercent = ClampConfidence(score)
		};
	}

	private static int CalculateMatchScore(RoadmapItem roadmapItem, UserStory story)
	{
		var roadmapTokens = GetNormalizedTokens($"{roadmapItem.Title} {roadmapItem.Description}");
		var storyTokens = GetNormalizedTokens($"{story.Title} {story.Description} {story.AcceptanceCriteria}");

		if (roadmapTokens.Count == 0 || storyTokens.Count == 0)
			return 0;

		var overlapCount = roadmapTokens.Count(token => storyTokens.Contains(token));
		if (overlapCount == 0)
			return 0;

		var denominator = Math.Max(roadmapTokens.Count, storyTokens.Count);
		var overlapRatio = (double)overlapCount / denominator;
		return ClampConfidence((int)Math.Round(overlapRatio * 100));
	}

	private static HashSet<string> GetNormalizedTokens(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return [];

		var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"the",
			"and",
			"for",
			"with",
			"from",
			"that",
			"this",
			"into",
			"your",
			"have"
		};

		var tokens = Regex
			.Matches(text.ToLowerInvariant(), "[a-z0-9]+")
			.Select(match => match.Value)
			.Where(token => token.Length >= 3 && !stopWords.Contains(token));

		return [.. tokens];
	}

	private static int ClampConfidence(int value) => Math.Clamp(value, 0, 100);

	private void TrimHistory()
	{
		while (_history.Count > MaxHistoryMessages)
		{
			// Keep the system prompt (index 0), remove the oldest non-system message.
			if (_history.Count > 1)
				_history.RemoveAt(1);
		}
	}

	private sealed record LoadedRoadmapState
	{
		public required string SourcePath { get; init; }
		public required DateTimeOffset LoadedAt { get; init; }
		public required IReadOnlyList<RoadmapItem> Items { get; init; }
	}

	private sealed record LinkExplanation
	{
		public required string Rationale { get; init; }
		public required int ConfidencePercent { get; init; }
	}
}