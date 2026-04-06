using System.Text.Json;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using MyOwnPo.Repositories;
using MyOwnPo.Repositories.Interfaces;
using MyOwnPo.Services;

using Xunit;

namespace MyOwnPo.UnitTests;

public class SessionHistoryServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public SessionHistoryServiceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task SaveSession_ShouldNot_ThrowWhenSaveFileFails()
    {
        var agent = CreateTestAgent();
        var repository = new Mock<IFileRepository>(MockBehavior.Strict);
        repository
            .Setup(mock => mock.SaveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk full"));
        var sut = new SessionHistoryService(repository.Object, agent, NullLogger<SessionHistoryService>.Instance);
        var session = await agent.CreateSessionAsync();

        var exception = await Record.ExceptionAsync(() => sut.SaveSession("default", session));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ResetSession_Should_CallDeleteFileWhenResettingSession()
    {
        var agent = CreateTestAgent();
        var repository = new Mock<IFileRepository>();
        repository
            .Setup(mock => mock.DeleteFile("default.json", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var sut = new SessionHistoryService(repository.Object, agent, NullLogger<SessionHistoryService>.Instance);

        await sut.ResetSession("default");

        repository.Verify(mock => mock.DeleteFile("default.json", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadSession_Should_DeleteFileAndCreateNewSessionWhenJsonIsMalformed()
    {
        var agent = CreateTestAgent();
        var repository = new Mock<IFileRepository>(MockBehavior.Strict);
        repository
            .Setup(mock => mock.LoadFile("default.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{ broken json }");
        repository
            .Setup(mock => mock.DeleteFile("default.json", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var sut = new SessionHistoryService(repository.Object, agent, NullLogger<SessionHistoryService>.Instance);

        var session = await sut.LoadSession("default");

        Assert.NotNull(session);
        repository.Verify(mock => mock.DeleteFile("default.json", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AIAgent CreateTestAgent()
    {
        var chatClient = new Mock<IChatClient>(MockBehavior.Loose);
        return chatClient.Object
            .AsBuilder()
            .BuildAIAgent(options: new()
            {
                Name = "SessionHistoryTestAgent",
                Description = "Agent used for session serialization tests"
            });
    }
}