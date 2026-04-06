using Azure.AI.OpenAI;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using MyOwnPo.App.Tools;
using MyOwnPo.Gateways;

namespace MyOwnPo.App.Agents;

public static class POAgentHelper
{
    public const string AgentName = "ProductOwnerBrainAgent";

    public static void AddPOAgent(this IServiceCollection services)
    {
        services.AddKeyedSingleton<AIAgent>(AgentName, (sp, _) =>
        {
            var settings = sp.GetRequiredService<IOptions<AzureOpenAiSettings>>().Value;
            var aiClient = sp.GetRequiredService<AzureOpenAIClient>();
            var chatClient = aiClient.GetChatClient(settings.DeploymentName).AsIChatClient();
            var backlogTools = sp.GetRequiredService<BacklogTools>();

            return chatClient
                .AsBuilder()
                .BuildAIAgent(
                    options: new()
                    {
                        Name = AgentName,
                        Description = "An agent that serves as a product owner assistant, helping to manage the backlog",
                        ChatOptions = new()
                        {
                            Instructions = """
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
                                """,
                            ResponseFormat = ChatResponseFormat.Text,
                            ModelId = settings.DeploymentName,
                            Temperature = 0.0f,
                            Tools = [
                                AIFunctionFactory.Create(backlogTools.GetBacklogStories)
                            ]
                        }
                    }
                );
        });
    }
}