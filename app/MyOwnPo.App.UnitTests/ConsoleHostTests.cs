using Moq;

using MyOwnPo.Models;
using MyOwnPo.Services;
using MyOwnPo.Services.Interfaces;

using Xunit;

namespace MyOwnPo.UnitTests;

public class ConsoleHostTests
{
	[Fact]
	public async Task HandleInput_ConnectCommand_CallsBacklogService()
	{
		var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
		backlogService
			.Setup(mock => mock.Connect())
			.ReturnsAsync(new List<UserStory> { Story("1", "Story A") });
		backlogService
			.Setup(mock => mock.GetStories())
			.Returns(new List<UserStory> { Story("1", "Story A") });
		var productOwnerBrainService = new Mock<IProductOwnerBrainService>(MockBehavior.Strict);
		var (input, output, sut) = CreateHost(backlogService.Object, productOwnerBrainService.Object, "connect\nexit\n");

		await sut.Run();

		backlogService.Verify(mock => mock.Connect(), Times.Once);
		Assert.Contains("Connected. Stories found: 1.", output.ToString());
	}

	[Fact]
	public async Task HandleInput_NaturalLanguage_ForwardsToAgent()
	{
		var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
		backlogService
			.Setup(mock => mock.GetStories())
			.Returns(new List<UserStory>
			{
				Story("1", "Story A"),
				Story("2", "Story B")
			});
		var productOwnerBrainService = new Mock<IProductOwnerBrainService>(MockBehavior.Strict);
		productOwnerBrainService
			.Setup(mock => mock.Chat("suggest priorities"))
			.ReturnsAsync("1. Story A\n2. Story B");
		var (input, output, sut) = CreateHost(backlogService.Object, productOwnerBrainService.Object, "suggest priorities\nexit\n");

		await sut.Run();

		productOwnerBrainService.Verify(mock => mock.Chat("suggest priorities"), Times.Once);
		Assert.Contains("1. Story A", output.ToString());
	}

	[Fact]
	public async Task HandleInput_RoadmapRequest_ForwardsToProductOwnerBrainService()
	{
		var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
		backlogService
			.Setup(mock => mock.GetStories())
			.Returns(new List<UserStory>
			{
				Story("1", "Story A"),
				Story("2", "Story B")
			});
		var productOwnerBrainService = new Mock<IProductOwnerBrainService>(MockBehavior.Strict);
		productOwnerBrainService
			.Setup(mock => mock.Chat("please analyze roadmap.md and link it to backlog"))
			.ReturnsAsync("Linked roadmap items: ...");
		var (input, output, sut) = CreateHost(
			backlogService.Object,
			productOwnerBrainService.Object,
			"please analyze roadmap.md and link it to backlog\nexit\n");

		await sut.Run();

		productOwnerBrainService.Verify(mock => mock.Chat("please analyze roadmap.md and link it to backlog"), Times.Once);
		Assert.Contains("Linked roadmap items", output.ToString());
	}

	[Fact]
	public async Task HandleInput_NaturalLanguageWithNoBacklog_ForwardsToProductOwnerBrain()
	{
		var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
		var productOwnerBrainService = new Mock<IProductOwnerBrainService>(MockBehavior.Strict);
		productOwnerBrainService
			.Setup(mock => mock.Chat("suggest priorities"))
			.ReturnsAsync("No stories loaded.");
		var (input, output, sut) = CreateHost(backlogService.Object, productOwnerBrainService.Object, "suggest priorities\nexit\n");

		await sut.Run();

		productOwnerBrainService.Verify(mock => mock.Chat("suggest priorities"), Times.Once);
		Assert.Contains("No stories loaded", output.ToString());
	}

	[Fact]
	public async Task HandleInput_ExitCommand_Exits()
	{
		var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
		var productOwnerBrainService = new Mock<IProductOwnerBrainService>(MockBehavior.Strict);
		var (input, output, sut) = CreateHost(backlogService.Object, productOwnerBrainService.Object, "exit\n");

		await sut.Run();

		Assert.Contains("Bye.", output.ToString());
	}

	[Fact]
	public async Task HandleInput_HelpCommand_ShowsHelp()
	{
		var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
		var productOwnerBrainService = new Mock<IProductOwnerBrainService>(MockBehavior.Strict);
		var (input, output, sut) = CreateHost(backlogService.Object, productOwnerBrainService.Object, "help\nexit\n");

		await sut.Run();

		var text = output.ToString();
		Assert.Contains("connect", text);
		Assert.Contains("refresh", text);
		Assert.Contains("AI Product Owner", text);
	}

	[Fact]
	public async Task HandleInput_RefreshCommand_CallsBacklogService()
	{
		var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
		backlogService
			.Setup(mock => mock.Refresh())
			.ReturnsAsync(new BacklogDiff
			{
				Added = [],
				Removed = [],
				Changed = []
			});
		var productOwnerBrainService = new Mock<IProductOwnerBrainService>(MockBehavior.Strict);
		var (input, output, sut) = CreateHost(backlogService.Object, productOwnerBrainService.Object, "refresh\nexit\n");

		await sut.Run();

		backlogService.Verify(mock => mock.Refresh(), Times.Once);
		Assert.Contains("Refresh complete", output.ToString());
	}

	private static (StringReader Input, StringWriter Output, ConsoleHost Host) CreateHost(
		IBacklogService backlogService,
		IProductOwnerBrainService productOwnerBrainService,
		string inputText,
		IProjectContextService? projectContextService = null)
	{
		var input = new StringReader(inputText);
		var output = new StringWriter();
		if (projectContextService is null)
		{
			var mock = new Mock<IProjectContextService>(MockBehavior.Loose);
			mock.Setup(m => m.LoadFromFile()).Returns(ContextLoadResult.NoFile);
			projectContextService = mock.Object;
		}
		var host = new ConsoleHost(backlogService, productOwnerBrainService, projectContextService, input, output);
		return (input, output, host);
	}

	private static UserStory Story(string id, string title) =>
		new()
		{
			Id = id,
			Title = title,
			Description = "Description",
			AcceptanceCriteria = "Acceptance",
			Priority = 1,
			Status = "New",
			Labels = ["Tag"]
		};

	[Fact]
	public async Task HandleInput_ContextSetCommand_SetsContext()
	{
		var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
		var productOwnerBrainService = new Mock<IProductOwnerBrainService>(MockBehavior.Strict);
		var contextService = new Mock<IProjectContextService>(MockBehavior.Strict);
		contextService
			.Setup(mock => mock.LoadFromFile())
			.Returns(ContextLoadResult.NoFile);
		contextService
			.Setup(mock => mock.SetContext(It.IsAny<ProjectContext>()));
		var (input, output, sut) = CreateHost(
			backlogService.Object,
			productOwnerBrainService.Object,
			"context set\nOur vision\nGrow revenue\nEnterprise teams\nPerformance\nNo budget\nexit\n",
			contextService.Object);

		await sut.Run();

		contextService.Verify(mock => mock.SetContext(It.Is<ProjectContext>(ctx =>
			ctx.Vision == "Our vision"
			&& ctx.BusinessGoals == "Grow revenue"
			&& ctx.TargetUsers == "Enterprise teams"
			&& ctx.SprintFocus == "Performance"
			&& ctx.Constraints == "No budget")), Times.Once);
		Assert.Contains("Project context updated", output.ToString());
	}

	[Fact]
	public async Task HandleInput_ContextShowCommand_DisplaysContext()
	{
		var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
		var productOwnerBrainService = new Mock<IProductOwnerBrainService>(MockBehavior.Strict);
		var contextService = new Mock<IProjectContextService>(MockBehavior.Strict);
		contextService
			.Setup(mock => mock.LoadFromFile())
			.Returns(ContextLoadResult.NoFile);
		contextService
			.Setup(mock => mock.GetContext())
			.Returns(new ProjectContext
			{
				Vision = "Best tool ever",
				BusinessGoals = "Revenue growth",
				TargetUsers = null,
				SprintFocus = "Onboarding",
				Constraints = null
			});
		var (input, output, sut) = CreateHost(
			backlogService.Object,
			productOwnerBrainService.Object,
			"context show\nexit\n",
			contextService.Object);

		await sut.Run();

		var text = output.ToString();
		Assert.Contains("Best tool ever", text);
		Assert.Contains("Revenue growth", text);
		Assert.Contains("Onboarding", text);
	}

	[Fact]
	public async Task HandleInput_ContextShowCommand_NoContext_ShowsMessage()
	{
		var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
		var productOwnerBrainService = new Mock<IProductOwnerBrainService>(MockBehavior.Strict);
		var contextService = new Mock<IProjectContextService>(MockBehavior.Strict);
		contextService
			.Setup(mock => mock.LoadFromFile())
			.Returns(ContextLoadResult.NoFile);
		contextService
			.Setup(mock => mock.GetContext())
			.Returns((ProjectContext?)null);
		var (input, output, sut) = CreateHost(
			backlogService.Object,
			productOwnerBrainService.Object,
			"context show\nexit\n",
			contextService.Object);

		await sut.Run();

		Assert.Contains("No project context set", output.ToString());
	}

	[Fact]
	public async Task HandleInput_ContextClearCommand_ClearsContext()
	{
		var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
		var productOwnerBrainService = new Mock<IProductOwnerBrainService>(MockBehavior.Strict);
		var contextService = new Mock<IProjectContextService>(MockBehavior.Strict);
		contextService
			.Setup(mock => mock.LoadFromFile())
			.Returns(ContextLoadResult.NoFile);
		contextService
			.Setup(mock => mock.ClearContext());
		var (input, output, sut) = CreateHost(
			backlogService.Object,
			productOwnerBrainService.Object,
			"context clear\nexit\n",
			contextService.Object);

		await sut.Run();

		contextService.Verify(mock => mock.ClearContext(), Times.Once);
		Assert.Contains("Project context cleared", output.ToString());
	}

	[Fact]
	public async Task Run_ContextFileExists_DisplaysLoadedMessage()
	{
		var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
		var productOwnerBrainService = new Mock<IProductOwnerBrainService>(MockBehavior.Strict);
		var contextService = new Mock<IProjectContextService>(MockBehavior.Strict);
		contextService
			.Setup(mock => mock.LoadFromFile())
			.Returns(ContextLoadResult.Loaded);
		contextService
			.Setup(mock => mock.GetContext())
			.Returns(new ProjectContext
			{
				Vision = "Best tool ever",
				BusinessGoals = "Revenue growth",
				TargetUsers = "Enterprise teams",
				SprintFocus = "Onboarding",
				Constraints = "Limited budget"
			});
		var (input, output, sut) = CreateHost(
			backlogService.Object,
			productOwnerBrainService.Object,
			"exit\n",
			contextService.Object);

		await sut.Run();

		var text = output.ToString();
		Assert.Contains("Project context loaded from file:", text);
		Assert.Contains("Best tool ever", text);
		Assert.Contains("Revenue growth", text);
		Assert.Contains("Enterprise teams", text);
		Assert.Contains("Onboarding", text);
		Assert.Contains("Limited budget", text);
	}

	[Fact]
	public async Task Run_NoContextFile_NoLoadMessage()
	{
		var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
		var productOwnerBrainService = new Mock<IProductOwnerBrainService>(MockBehavior.Strict);
		var (input, output, sut) = CreateHost(
			backlogService.Object,
			productOwnerBrainService.Object,
			"exit\n");

		await sut.Run();

		var text = output.ToString();
		Assert.DoesNotContain("Project context loaded from file:", text);
		Assert.DoesNotContain("Warning:", text);
	}

	[Fact]
	public async Task Run_MalformedContextFile_DisplaysWarning()
	{
		var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
		var productOwnerBrainService = new Mock<IProductOwnerBrainService>(MockBehavior.Strict);
		var contextService = new Mock<IProjectContextService>(MockBehavior.Strict);
		contextService
			.Setup(mock => mock.LoadFromFile())
			.Returns(ContextLoadResult.Malformed);
		var (input, output, sut) = CreateHost(
			backlogService.Object,
			productOwnerBrainService.Object,
			"exit\n",
			contextService.Object);

		await sut.Run();

		var text = output.ToString();
		Assert.Contains("Warning: Could not read project context file. Starting without context.", text);
	}
}