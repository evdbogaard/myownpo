using Moq;

using MyOwnPo.Models;
using MyOwnPo.Services;

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
        var prioritizationService = new Mock<IPrioritizationService>(MockBehavior.Strict);
        var (input, output, sut) = CreateHost(backlogService.Object, prioritizationService.Object, "connect\nexit\n");

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
        var prioritizationService = new Mock<IPrioritizationService>(MockBehavior.Strict);
        prioritizationService
            .Setup(mock => mock.Chat("suggest priorities"))
            .ReturnsAsync("1. Story A\n2. Story B");
        var (input, output, sut) = CreateHost(backlogService.Object, prioritizationService.Object, "suggest priorities\nexit\n");

        await sut.Run();

        prioritizationService.Verify(mock => mock.Chat("suggest priorities"), Times.Once);
        Assert.Contains("1. Story A", output.ToString());
    }

    [Fact]
    public async Task HandleInput_NaturalLanguageWithNoBacklog_PromptsToConnect()
    {
        var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
        backlogService
            .Setup(mock => mock.GetStories())
            .Returns(new List<UserStory>());
        var prioritizationService = new Mock<IPrioritizationService>(MockBehavior.Strict);
        var (input, output, sut) = CreateHost(backlogService.Object, prioritizationService.Object, "suggest priorities\nexit\n");

        await sut.Run();

        prioritizationService.Verify(mock => mock.Chat(It.IsAny<string>()), Times.Never);
        Assert.Contains("No backlog loaded", output.ToString());
        Assert.Contains("connect", output.ToString());
    }

    [Fact]
    public async Task HandleInput_ExitCommand_Exits()
    {
        var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
        var prioritizationService = new Mock<IPrioritizationService>(MockBehavior.Strict);
        var (input, output, sut) = CreateHost(backlogService.Object, prioritizationService.Object, "exit\n");

        await sut.Run();

        Assert.Contains("Bye.", output.ToString());
    }

    [Fact]
    public async Task HandleInput_HelpCommand_ShowsHelp()
    {
        var backlogService = new Mock<IBacklogService>(MockBehavior.Strict);
        var prioritizationService = new Mock<IPrioritizationService>(MockBehavior.Strict);
        var (input, output, sut) = CreateHost(backlogService.Object, prioritizationService.Object, "help\nexit\n");

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
        var prioritizationService = new Mock<IPrioritizationService>(MockBehavior.Strict);
        var (input, output, sut) = CreateHost(backlogService.Object, prioritizationService.Object, "refresh\nexit\n");

        await sut.Run();

        backlogService.Verify(mock => mock.Refresh(), Times.Once);
        Assert.Contains("Refresh complete", output.ToString());
    }

    private static (StringReader Input, StringWriter Output, ConsoleHost Host) CreateHost(
        IBacklogService backlogService,
        IPrioritizationService prioritizationService,
        string inputText)
    {
        var input = new StringReader(inputText);
        var output = new StringWriter();
        var host = new ConsoleHost(backlogService, prioritizationService, input, output);
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
}