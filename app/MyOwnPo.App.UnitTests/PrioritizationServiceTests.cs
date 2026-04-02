using System.Reflection;
using System.Text.Json;

using Microsoft.Extensions.AI;

using Moq;

using MyOwnPo.Models;
using MyOwnPo.Services;

using Xunit;

namespace MyOwnPo.UnitTests;

public class PrioritizationServiceTests
{
	[Fact]
	public async Task Chat_GeneralMessage_ReturnsAgentResponse()
	{
		var chatClient = CreateChatClientMock("Here are my suggestions.");
		var backlogService = CreateBacklogServiceMock([]);
		var contextService = CreateProjectContextServiceMock();
		var roadmapLoader = CreateRoadmapFileLoaderMock();
		var roadmapParser = CreateRoadmapParserMock();
		var sut = new PrioritizationService(chatClient.Object, backlogService.Object, contextService.Object, roadmapLoader.Object, roadmapParser.Object);

		var result = await sut.Chat("suggest priorities");

		Assert.Equal("Here are my suggestions.", result);
	}

	[Fact]
	public async Task Chat_CallsGetResponseAsyncWithUserMessage()
	{
		IEnumerable<ChatMessage>? capturedMessages = null;
		var chatClient = new Mock<IChatClient>(MockBehavior.Strict);
		chatClient
			.Setup(mock => mock.GetResponseAsync(
				It.IsAny<IList<ChatMessage>>(),
				It.IsAny<ChatOptions>(),
				It.IsAny<CancellationToken>()))
			.Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((messages, _, _) =>
				capturedMessages = messages)
			.ReturnsAsync(CreateChatResponse("response"));
		chatClient
			.Setup(mock => mock.Dispose());
		var backlogService = CreateBacklogServiceMock([]);
		var contextService = CreateProjectContextServiceMock();
		var roadmapLoader = CreateRoadmapFileLoaderMock();
		var roadmapParser = CreateRoadmapParserMock();
		var sut = new PrioritizationService(chatClient.Object, backlogService.Object, contextService.Object, roadmapLoader.Object, roadmapParser.Object);

		await sut.Chat("prioritize my backlog");

		Assert.NotNull(capturedMessages);
		Assert.Contains(capturedMessages, message =>
			message.Role == ChatRole.User
			&& message.Text == "prioritize my backlog");
	}

	[Fact]
	public async Task Chat_FirstMessage_IncludesSystemPrompt()
	{
		IEnumerable<ChatMessage>? capturedMessages = null;
		var chatClient = new Mock<IChatClient>(MockBehavior.Strict);
		chatClient
			.Setup(mock => mock.GetResponseAsync(
				It.IsAny<IList<ChatMessage>>(),
				It.IsAny<ChatOptions>(),
				It.IsAny<CancellationToken>()))
			.Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((messages, _, _) =>
				capturedMessages = messages)
			.ReturnsAsync(CreateChatResponse("response"));
		chatClient
			.Setup(mock => mock.Dispose());
		var backlogService = CreateBacklogServiceMock([]);
		var contextService = CreateProjectContextServiceMock();
		var roadmapLoader = CreateRoadmapFileLoaderMock();
		var roadmapParser = CreateRoadmapParserMock();
		var sut = new PrioritizationService(chatClient.Object, backlogService.Object, contextService.Object, roadmapLoader.Object, roadmapParser.Object);

		await sut.Chat("hello");

		Assert.NotNull(capturedMessages);
		var messages = capturedMessages.ToList();
		Assert.Equal(ChatRole.System, messages[0].Role);
		Assert.Contains("AI Product Owner", messages[0].Text);
	}

	[Fact]
	public async Task Chat_MultipleCalls_AccumulatesHistory()
	{
		IEnumerable<ChatMessage>? capturedMessages = null;
		var chatClient = new Mock<IChatClient>(MockBehavior.Strict);
		chatClient
			.Setup(mock => mock.GetResponseAsync(
				It.IsAny<IList<ChatMessage>>(),
				It.IsAny<ChatOptions>(),
				It.IsAny<CancellationToken>()))
			.Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((messages, _, _) =>
				capturedMessages = messages)
			.ReturnsAsync(CreateChatResponse("response"));
		chatClient
			.Setup(mock => mock.Dispose());
		var backlogService = CreateBacklogServiceMock([]);
		var contextService = CreateProjectContextServiceMock();
		var roadmapLoader = CreateRoadmapFileLoaderMock();
		var roadmapParser = CreateRoadmapParserMock();
		var sut = new PrioritizationService(chatClient.Object, backlogService.Object, contextService.Object, roadmapLoader.Object, roadmapParser.Object);

		await sut.Chat("first message");
		await sut.Chat("second message");

		Assert.NotNull(capturedMessages);
		Assert.True(capturedMessages.Count() >= 4);
	}

	[Fact]
	public async Task Chat_NullResponseText_ReturnsEmptyString()
	{
		var chatClient = CreateChatClientMock(null);
		var backlogService = CreateBacklogServiceMock([]);
		var contextService = CreateProjectContextServiceMock();
		var roadmapLoader = CreateRoadmapFileLoaderMock();
		var roadmapParser = CreateRoadmapParserMock();
		var sut = new PrioritizationService(chatClient.Object, backlogService.Object, contextService.Object, roadmapLoader.Object, roadmapParser.Object);

		var result = await sut.Chat("test");

		Assert.Equal(string.Empty, result);
	}

	[Fact]
	public async Task Chat_ConfiguresChatOptionsWithTools()
	{
		ChatOptions? capturedOptions = null;
		var chatClient = new Mock<IChatClient>(MockBehavior.Strict);
		chatClient
			.Setup(mock => mock.GetResponseAsync(
				It.IsAny<IList<ChatMessage>>(),
				It.IsAny<ChatOptions>(),
				It.IsAny<CancellationToken>()))
			.Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((_, options, _) =>
				capturedOptions = options)
			.ReturnsAsync(CreateChatResponse("response"));
		chatClient
			.Setup(mock => mock.Dispose());
		var backlogService = CreateBacklogServiceMock([]);
		var contextService = CreateProjectContextServiceMock();
		var roadmapLoader = CreateRoadmapFileLoaderMock();
		var roadmapParser = CreateRoadmapParserMock();
		var sut = new PrioritizationService(chatClient.Object, backlogService.Object, contextService.Object, roadmapLoader.Object, roadmapParser.Object);

		await sut.Chat("test");

		Assert.NotNull(capturedOptions);
		Assert.NotNull(capturedOptions.Tools);
		Assert.Contains(capturedOptions.Tools, tool =>
			tool is AIFunction function && function.Name == "GetBacklogStories");
		Assert.Contains(capturedOptions.Tools, tool =>
			tool is AIFunction function && function.Name == "LoadRoadmap");
		Assert.Contains(capturedOptions.Tools, tool =>
			tool is AIFunction function && function.Name == "EvaluateRoadmapStoryLinks");
	}

	[Fact]
	public async Task Chat_ConfiguresChatOptionsWithGetProjectContextTool()
	{
		ChatOptions? capturedOptions = null;
		var chatClient = new Mock<IChatClient>(MockBehavior.Strict);
		chatClient
			.Setup(mock => mock.GetResponseAsync(
				It.IsAny<IList<ChatMessage>>(),
				It.IsAny<ChatOptions>(),
				It.IsAny<CancellationToken>()))
			.Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((_, options, _) =>
				capturedOptions = options)
			.ReturnsAsync(CreateChatResponse("response"));
		chatClient
			.Setup(mock => mock.Dispose());
		var backlogService = CreateBacklogServiceMock([]);
		var contextService = CreateProjectContextServiceMock();
		var roadmapLoader = CreateRoadmapFileLoaderMock();
		var roadmapParser = CreateRoadmapParserMock();
		var sut = new PrioritizationService(chatClient.Object, backlogService.Object, contextService.Object, roadmapLoader.Object, roadmapParser.Object);

		await sut.Chat("test");

		Assert.NotNull(capturedOptions);
		Assert.NotNull(capturedOptions.Tools);
		Assert.Contains(capturedOptions.Tools, tool =>
			tool is AIFunction function && function.Name == "GetProjectContext");
	}

	[Fact]
	public async Task Chat_RoadmapRequestWithPath_LoadsRoadmapAndReturnsLinkRecommendations()
	{
		var roadmapItems = new List<RoadmapItem>
		{
			new() { Id = "roadmap-001", Title = "Improve onboarding" }
		};
		var stories = new List<UserStory>
		{
			Story("42", "Improve user onboarding flow", "Optimize signup steps")
		};
		var chatClient = CreateChatClientMock("{\"rationale\":\"The story delivers onboarding improvements in this roadmap area.\",\"confidencePercent\":85}");
		var backlogService = CreateBacklogServiceMock(stories);
		var contextService = CreateProjectContextServiceMock();
		var roadmapLoader = new Mock<IRoadmapFileLoader>(MockBehavior.Strict);
		roadmapLoader.Setup(loader => loader.Load("custom-roadmap.md")).Returns("# Improve onboarding");
		var roadmapParser = new Mock<IRoadmapParser>(MockBehavior.Strict);
		roadmapParser.Setup(parser => parser.Parse(It.IsAny<string>())).Returns(roadmapItems);
		var sut = new PrioritizationService(chatClient.Object, backlogService.Object, contextService.Object, roadmapLoader.Object, roadmapParser.Object);

		var loadResult = InvokeLoadRoadmap(sut, "custom-roadmap.md");
		var analysisJson = await InvokeEvaluateRoadmapStoryLinks(sut);
		var analysis = DeserializeAnalysis(analysisJson);

		Assert.Contains("custom-roadmap.md", loadResult);
		Assert.Single(analysis.LinkedStories);
		Assert.Equal("42", analysis.LinkedStories[0].StoryId);
		Assert.Empty(analysis.UnlinkedRoadmapItems);
		roadmapLoader.Verify(loader => loader.Load("custom-roadmap.md"), Times.Once);
	}

	[Fact]
	public async Task Chat_RoadmapRequest_ItemHasNoStory_ListsItemAsUnlinked()
	{
		var roadmapItems = new List<RoadmapItem>
		{
			new() { Id = "roadmap-001", Title = "Launch billing portal" }
		};
		var chatClient = CreateChatClientMock("{\"rationale\":\"n/a\",\"confidencePercent\":0}");
		var backlogService = CreateBacklogServiceMock([]);
		var contextService = CreateProjectContextServiceMock();
		var roadmapLoader = CreateRoadmapFileLoaderMock("# Launch billing portal");
		var roadmapParser = CreateRoadmapParserMock(roadmapItems);
		var sut = new PrioritizationService(chatClient.Object, backlogService.Object, contextService.Object, roadmapLoader.Object, roadmapParser.Object);

		InvokeLoadRoadmap(sut);
		var analysisJson = await InvokeEvaluateRoadmapStoryLinks(sut);
		var analysis = DeserializeAnalysis(analysisJson);

		Assert.Empty(analysis.LinkedStories);
		Assert.Single(analysis.UnlinkedRoadmapItems);
		Assert.Equal("roadmap-001", analysis.UnlinkedRoadmapItems[0].Id);
	}

	[Fact]
	public async Task Chat_RoadmapRequest_StoryAlreadyAssigned_DoesNotLinkStoryTwice()
	{
		var roadmapItems = new List<RoadmapItem>
		{
			new() { Id = "roadmap-001", Title = "Improve onboarding" },
			new() { Id = "roadmap-002", Title = "Onboarding analytics" }
		};
		var stories = new List<UserStory>
		{
			Story("42", "Improve user onboarding flow", "Optimize signup steps and metrics")
		};
		var chatClient = CreateChatClientMock("{\"rationale\":\"Strong overlap with onboarding outcomes.\",\"confidencePercent\":78}");
		var backlogService = CreateBacklogServiceMock(stories);
		var contextService = CreateProjectContextServiceMock();
		var roadmapLoader = CreateRoadmapFileLoaderMock("# Improve onboarding\n# Onboarding analytics");
		var roadmapParser = CreateRoadmapParserMock(roadmapItems);
		var sut = new PrioritizationService(chatClient.Object, backlogService.Object, contextService.Object, roadmapLoader.Object, roadmapParser.Object);

		InvokeLoadRoadmap(sut);
		var analysisJson = await InvokeEvaluateRoadmapStoryLinks(sut);
		var analysis = DeserializeAnalysis(analysisJson);

		Assert.Single(analysis.LinkedStories);
		Assert.Single(analysis.UnlinkedRoadmapItems);
		Assert.Equal("42", analysis.LinkedStories[0].StoryId);
	}

	[Fact]
	public async Task Chat_RoadmapRequest_ProducesBusinessReadableRationaleForEachLink()
	{
		var roadmapItems = new List<RoadmapItem>
		{
			new() { Id = "roadmap-001", Title = "Improve onboarding" }
		};
		var stories = new List<UserStory>
		{
			Story("42", "Improve user onboarding flow", "Optimize signup steps")
		};
		var chatClient = CreateChatClientMock("{\"rationale\":\"This story supports the roadmap objective by reducing onboarding friction for first-time users.\",\"confidencePercent\":88}");
		var backlogService = CreateBacklogServiceMock(stories);
		var contextService = CreateProjectContextServiceMock();
		var roadmapLoader = CreateRoadmapFileLoaderMock("# Improve onboarding");
		var roadmapParser = CreateRoadmapParserMock(roadmapItems);
		var sut = new PrioritizationService(chatClient.Object, backlogService.Object, contextService.Object, roadmapLoader.Object, roadmapParser.Object);

		InvokeLoadRoadmap(sut);
		var analysisJson = await InvokeEvaluateRoadmapStoryLinks(sut);
		var analysis = DeserializeAnalysis(analysisJson);

		Assert.Single(analysis.LinkedStories);
		Assert.NotNull(analysis.LinkedStories[0].Rationale);
		Assert.NotEmpty(analysis.LinkedStories[0].Rationale.Trim());
		Assert.InRange(analysis.LinkedStories[0].ConfidencePercent, 0, 100);
	}

	[Fact]
	public async Task Chat_FollowUpQuestionAfterRoadmapLoad_ReusesLoadedRoadmapInMemory()
	{
		var roadmapItems = new List<RoadmapItem>
		{
			new() { Id = "roadmap-001", Title = "Improve onboarding" }
		};
		var stories = new List<UserStory>
		{
			Story("42", "Improve user onboarding flow", "Optimize signup steps")
		};
		var chatClient = CreateChatClientMock("{\"rationale\":\"Strong overlap.\",\"confidencePercent\":70}");
		var backlogService = CreateBacklogServiceMock(stories);
		var contextService = CreateProjectContextServiceMock();
		var roadmapLoader = new Mock<IRoadmapFileLoader>(MockBehavior.Strict);
		roadmapLoader.Setup(loader => loader.Load(It.IsAny<string>())).Returns("# Improve onboarding");
		var roadmapParser = new Mock<IRoadmapParser>(MockBehavior.Strict);
		roadmapParser.Setup(parser => parser.Parse(It.IsAny<string>())).Returns(roadmapItems);
		var sut = new PrioritizationService(chatClient.Object, backlogService.Object, contextService.Object, roadmapLoader.Object, roadmapParser.Object);

		InvokeLoadRoadmap(sut);
		var firstRunJson = await InvokeEvaluateRoadmapStoryLinks(sut);
		var secondRunJson = await InvokeEvaluateRoadmapStoryLinks(sut);
		var firstRun = DeserializeAnalysis(firstRunJson);
		var secondRun = DeserializeAnalysis(secondRunJson);

		Assert.Single(firstRun.LinkedStories);
		Assert.Single(secondRun.LinkedStories);
		roadmapLoader.Verify(loader => loader.Load(It.IsAny<string>()), Times.Once);
		roadmapParser.Verify(parser => parser.Parse(It.IsAny<string>()), Times.Once);
	}

	private static Mock<IChatClient> CreateChatClientMock(string? responseText)
	{
		var mock = new Mock<IChatClient>(MockBehavior.Strict);
		mock
			.Setup(client => client.GetResponseAsync(
				It.IsAny<IList<ChatMessage>>(),
				It.IsAny<ChatOptions>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(CreateChatResponse(responseText));
		mock
			.Setup(client => client.Dispose());
		return mock;
	}

	private static ChatResponse CreateChatResponse(string? text)
	{
		var message = new ChatMessage(ChatRole.Assistant, text);
		return new ChatResponse(message);
	}

	private static Mock<IBacklogService> CreateBacklogServiceMock(IReadOnlyList<UserStory> stories)
	{
		var mock = new Mock<IBacklogService>(MockBehavior.Strict);
		mock.Setup(service => service.GetStories()).Returns(stories);
		return mock;
	}

	private static Mock<IProjectContextService> CreateProjectContextServiceMock(ProjectContext? context = null)
	{
		var mock = new Mock<IProjectContextService>(MockBehavior.Strict);
		mock.Setup(service => service.GetContext()).Returns(context);
		mock.Setup(service => service.HasContext).Returns(context is not null);
		return mock;
	}

	private static Mock<IRoadmapFileLoader> CreateRoadmapFileLoaderMock(string markdown = "# Roadmap")
	{
		var mock = new Mock<IRoadmapFileLoader>(MockBehavior.Strict);
		mock.Setup(loader => loader.Load(It.IsAny<string>())).Returns(markdown);
		return mock;
	}

	private static Mock<IRoadmapParser> CreateRoadmapParserMock(IReadOnlyList<RoadmapItem>? items = null)
	{
		var mock = new Mock<IRoadmapParser>(MockBehavior.Strict);
		mock.Setup(parser => parser.Parse(It.IsAny<string>())).Returns(items ?? []);
		return mock;
	}

	private static UserStory Story(string id, string title, string description) =>
		new()
		{
			Id = id,
			Title = title,
			Description = description,
			AcceptanceCriteria = "Acceptance",
			Priority = 1,
			Status = "New",
			Labels = ["Tag"]
		};

	private static string InvokeLoadRoadmap(PrioritizationService sut, string? filePath = null)
	{
		var method = typeof(PrioritizationService)
			.GetMethod("LoadRoadmap", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var value = method.Invoke(sut, [filePath]);
		return Assert.IsType<string>(value);
	}

	private static async Task<string> InvokeEvaluateRoadmapStoryLinks(PrioritizationService sut)
	{
		var method = typeof(PrioritizationService)
			.GetMethod("EvaluateRoadmapStoryLinks", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = method.Invoke(sut, null);
		var typedTask = Assert.IsType<Task<string>>(task);
		return await typedTask;
	}

	private static RoadmapAnalysisResult DeserializeAnalysis(string json)
	{
		var model = JsonSerializer.Deserialize<RoadmapAnalysisResult>(json);
		Assert.NotNull(model);
		return model;
	}
}