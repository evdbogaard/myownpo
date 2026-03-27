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
        var sut = new PrioritizationService(chatClient.Object, backlogService.Object);

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
        var sut = new PrioritizationService(chatClient.Object, backlogService.Object);

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
        var sut = new PrioritizationService(chatClient.Object, backlogService.Object);

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
        var sut = new PrioritizationService(chatClient.Object, backlogService.Object);

        await sut.Chat("first message");
        await sut.Chat("second message");

        Assert.NotNull(capturedMessages);
        // System prompt + "first message" + assistant response + "second message"
        Assert.True(capturedMessages.Count() >= 4);
    }

    [Fact]
    public async Task Chat_NullResponseText_ReturnsEmptyString()
    {
        var chatClient = CreateChatClientMock(null);
        var backlogService = CreateBacklogServiceMock([]);
        var sut = new PrioritizationService(chatClient.Object, backlogService.Object);

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
        var sut = new PrioritizationService(chatClient.Object, backlogService.Object);

        await sut.Chat("test");

        Assert.NotNull(capturedOptions);
        Assert.NotNull(capturedOptions.Tools);
        Assert.Contains(capturedOptions.Tools, tool =>
            tool is AIFunction function && function.Name == "GetBacklogStories");
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
}