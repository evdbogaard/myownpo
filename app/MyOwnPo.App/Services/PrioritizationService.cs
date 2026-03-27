using System.ComponentModel;
using System.Text.Json;

using Microsoft.Extensions.AI;

using MyOwnPo.Models;

namespace MyOwnPo.Services;

public class PrioritizationService : IPrioritizationService
{
	private const int MaxHistoryMessages = 20;

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
		""";

	private readonly IChatClient _chatClient;
	private readonly IBacklogService _backlogService;
	private readonly List<ChatMessage> _history = [];
	private readonly ChatOptions _chatOptions;

	public PrioritizationService(IChatClient chatClient, IBacklogService backlogService)
	{
		_chatClient = chatClient;
		_backlogService = backlogService;

		_history.Add(new ChatMessage(ChatRole.System, SystemPrompt));

		_chatOptions = new ChatOptions
		{
			Tools = [AIFunctionFactory.Create(GetBacklogStories, "GetBacklogStories", "Retrieves all user stories currently loaded from the backlog.")]
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

	private void TrimHistory()
	{
		while (_history.Count > MaxHistoryMessages)
		{
			// Keep the system prompt (index 0), remove the oldest non-system message.
			if (_history.Count > 1)
				_history.RemoveAt(1);
		}
	}
}