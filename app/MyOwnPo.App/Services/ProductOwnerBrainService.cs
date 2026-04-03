using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using MyOwnPo.App.Agents;
using MyOwnPo.Models;
using MyOwnPo.Services.Interfaces;

namespace MyOwnPo.Services;

public class ProductOwnerBrainService : IProductOwnerBrainService
{
	private const int MaxHistoryMessages = 20;
	private const string DefaultRoadmapPath = "roadmap.md";

	private static readonly string SystemPrompt = """
		You are the Product Owner brain for this console app, an expert in agile product management, user story prioritization, and backlog grooming..

		## Role
		- Accept free-text product questions and infer intent.
		- Use tools when data is needed; do not guess when a tool can provide facts.
		- Keep answers practical, concise, and actionable for product management.

		## Rules
		- You operate in suggestion-only mode. Never claim to have changed the backlog.
		- For prioritization suggestions, rank stories from highest to lowest and explain each rank.
		- If backlog data is missing, explain what command the user should run next.
		- Check project context with GetProjectContext whenever it can improve quality.
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
	private readonly AIAgent _agent;
	private readonly List<ChatMessage> _history = [];
	private readonly ChatOptions _chatOptions;
	private bool _hasAttemptedBacklogBootstrap;
	private LoadedRoadmapState? _loadedRoadmap;

	public ProductOwnerBrainService(
		IChatClient chatClient,
		IBacklogService backlogService,
		IProjectContextService projectContextService,
		IRoadmapFileLoader roadmapFileLoader,
		IRoadmapParser roadmapParser,
		[FromKeyedServices(POAgentHelper.AgentName)] AIAgent agent)
	{
		_chatClient = chatClient;
		_backlogService = backlogService;
		_projectContextService = projectContextService;
		_roadmapFileLoader = roadmapFileLoader;
		_roadmapParser = roadmapParser;
		_agent = agent;
		_history.Add(new ChatMessage(ChatRole.System, SystemPrompt));

		_chatOptions = new ChatOptions
		{
			Tools =
			[
				AIFunctionFactory.Create(GetBacklogStories),
				AIFunctionFactory.Create(GetBacklogStoryById),
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

		var agentResponse = await _agent.RunAsync(_history);
		var test = agentResponse.AsChatResponse();

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

	[Description("Retrieves backlog stories from memory. If empty, tries one automatic load attempt for this session.")]
	private async Task<string> GetBacklogStories()
	{
		var stories = await EnsureBacklogStories();

		return JsonSerializer.Serialize(new
		{
			Count = stories.Count,
			Stories = stories.Select(story => new
			{
				story.Id,
				story.Title,
				story.Description,
				story.AcceptanceCriteria,
				story.Priority,
				story.Labels,
				story.Status,
				story.MissingFields
			}),
			Message = stories.Count == 0
				? "No backlog stories available. Run connect to load stories manually."
				: null
		});
	}

	[Description("Retrieves one backlog story by id. If backlog is empty, tries one automatic load attempt for this session.")]
	private async Task<string> GetBacklogStoryById(string storyId)
	{
		var normalizedStoryId = storyId?.Trim();
		if (string.IsNullOrWhiteSpace(normalizedStoryId))
		{
			return JsonSerializer.Serialize(new
			{
				Status = "invalid-request",
				Message = "Story id is required."
			});
		}

		var stories = await EnsureBacklogStories();
		var story = stories.FirstOrDefault(candidate =>
			string.Equals(candidate.Id, normalizedStoryId, StringComparison.OrdinalIgnoreCase));

		if (story is null)
		{
			return JsonSerializer.Serialize(new
			{
				Status = "not-found",
				StoryId = normalizedStoryId,
				Message = "Story not found in the loaded backlog."
			});
		}

		return JsonSerializer.Serialize(new
		{
			Status = "ok",
			Story = new
			{
				story.Id,
				story.Title,
				story.Description,
				story.AcceptanceCriteria,
				story.Priority,
				story.Labels,
				story.Status,
				story.MissingFields
			}
		});
	}

	private async Task<IReadOnlyList<UserStory>> EnsureBacklogStories()
	{
		var stories = _backlogService.GetStories();
		if (stories.Count > 0)
			return stories;

		if (_hasAttemptedBacklogBootstrap)
			return stories;

		_hasAttemptedBacklogBootstrap = true;
		try
		{
			return await _backlogService.Connect();
		}
		catch
		{
			return _backlogService.GetStories();
		}
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

			var explanation = await GenerateBusinessRationale(roadmapItem, candidate.Story, candidate.Score);
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

	private async Task<LinkExplanation> GenerateBusinessRationale(RoadmapItem roadmapItem, UserStory story, int score)
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