using Moq;

using MyOwnPo.Gateways;
using MyOwnPo.Models;
using MyOwnPo.Services;

using Xunit;

namespace MyOwnPo.UnitTests;

public class BacklogServiceTests
{
	[Fact]
	public async Task Connect_WhenBacklogHasStories_ReturnsStories()
	{
		var stories = new[] { Story("1", "Story A"), Story("2", "Story B") };
		var gateway = CreateGatewayMock(stories);
		var sut = new BacklogService(gateway.Object);

		var result = await sut.Connect();

		Assert.Equal(2, result.Count);
		Assert.Equal("Story A", result[0].Title);
		Assert.Equal("Story B", result[1].Title);
	}

	[Fact]
	public async Task Connect_WhenBacklogIsEmpty_ReturnsEmptyList()
	{
		var gateway = CreateGatewayMock([]);
		var sut = new BacklogService(gateway.Object);

		var result = await sut.Connect();

		Assert.Empty(result);
	}

	[Fact]
	public async Task Connect_WhenBacklogHasSingleStory_ReturnsSingleStory()
	{
		var gateway = CreateGatewayMock([Story("1", "Only story")]);
		var sut = new BacklogService(gateway.Object);

		var result = await sut.Connect();

		Assert.Single(result);
	}

	[Fact]
	public async Task Connect_WhenBacklogHasMoreThan100Stories_ThrowsBacklogCapExceededException()
	{
		var stories = Enumerable.Range(1, 101)
			.Select(index => Story(index.ToString(), $"Story {index}"))
			.ToList();
		var gateway = CreateGatewayMock(stories);
		var sut = new BacklogService(gateway.Object);

		var exception = await Assert.ThrowsAsync<BacklogCapExceededException>(() => sut.Connect());
		Assert.Equal(101, exception.StoryCount);
	}

	[Fact]
	public async Task Connect_WhenGatewayThrows_ThrowsExceptionWithUsefulMessage()
	{
		var gateway = new Mock<IBacklogGateway>(MockBehavior.Strict);
		gateway
			.Setup(mock => mock.ReadStories())
			.ThrowsAsync(new InvalidOperationException("Verify AzureDevOps:Pat and organization URL."));
		var sut = new BacklogService(gateway.Object);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Connect());
		Assert.Contains("AzureDevOps:Pat", exception.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(false, false, false, false, 4)]
	[InlineData(false, false, false, true, 3)]
	[InlineData(false, false, true, false, 3)]
	[InlineData(false, false, true, true, 2)]
	[InlineData(false, true, false, false, 3)]
	[InlineData(false, true, false, true, 2)]
	[InlineData(false, true, true, false, 2)]
	[InlineData(false, true, true, true, 1)]
	[InlineData(true, false, false, false, 3)]
	[InlineData(true, false, false, true, 2)]
	[InlineData(true, false, true, false, 2)]
	[InlineData(true, false, true, true, 1)]
	[InlineData(true, true, false, false, 2)]
	[InlineData(true, true, false, true, 1)]
	[InlineData(true, true, true, false, 1)]
	[InlineData(true, true, true, true, 0)]
	public async Task Connect_WhenStoriesHaveMissingFields_PopulatesMissingFields(
		bool hasDescription,
		bool hasAcceptanceCriteria,
		bool hasPriority,
		bool hasStatus,
		int expectedMissingCount)
	{
		var gateway = CreateGatewayMock([
			new UserStory
			{
				Id = "1",
				Title = "Incomplete",
				Description = hasDescription ? "Description" : null,
				AcceptanceCriteria = hasAcceptanceCriteria ? "Acceptance" : null,
				Priority = hasPriority ? 1 : null,
				Status = hasStatus ? "New" : null,
				Labels = []
			}
		]);
		var sut = new BacklogService(gateway.Object);

		var result = await sut.Connect();

		Assert.Equal(expectedMissingCount, result[0].MissingFields.Count);
	}

	[Fact]
	public async Task GetStories_AfterConnect_ReturnsStoredStories()
	{
		var gateway = CreateGatewayMock([Story("1", "Story A")]);
		var sut = new BacklogService(gateway.Object);
		await sut.Connect();

		var stories = sut.GetStories();

		Assert.Single(stories);
	}

	[Fact]
	public async Task Refresh_WhenStoriesAdded_ReportsAdded()
	{
		var gateway = new Mock<IBacklogGateway>(MockBehavior.Strict);
		gateway.SetupSequence(mock => mock.ReadStories())
			.ReturnsAsync([Story("1", "Story A")])
			.ReturnsAsync([Story("1", "Story A"), Story("2", "Story B")]);
		var sut = new BacklogService(gateway.Object);
		await sut.Connect();

		var diff = await sut.Refresh();

		Assert.Single(diff.Added);
		Assert.Contains("Story B", diff.Added);
	}

	[Fact]
	public async Task Refresh_WhenStoriesRemoved_ReportsRemoved()
	{
		var gateway = new Mock<IBacklogGateway>(MockBehavior.Strict);
		gateway.SetupSequence(mock => mock.ReadStories())
			.ReturnsAsync([Story("1", "Story A"), Story("2", "Story B")])
			.ReturnsAsync([Story("1", "Story A")]);
		var sut = new BacklogService(gateway.Object);
		await sut.Connect();

		var diff = await sut.Refresh();

		Assert.Single(diff.Removed);
		Assert.Contains("Story B", diff.Removed);
	}

	[Fact]
	public async Task Refresh_WhenStoriesChanged_ReportsChanged()
	{
		var gateway = new Mock<IBacklogGateway>(MockBehavior.Strict);
		gateway.SetupSequence(mock => mock.ReadStories())
			.ReturnsAsync([Story("1", "Story A", description: "A")])
			.ReturnsAsync([Story("1", "Story A", description: "Changed")]);
		var sut = new BacklogService(gateway.Object);
		await sut.Connect();

		var diff = await sut.Refresh();

		Assert.Single(diff.Changed);
		Assert.Contains("Story A", diff.Changed);
	}

	[Fact]
	public async Task Refresh_WhenNoChanges_ReportsNoChanges()
	{
		var gateway = new Mock<IBacklogGateway>(MockBehavior.Strict);
		gateway.SetupSequence(mock => mock.ReadStories())
			.ReturnsAsync([Story("1", "Story A")])
			.ReturnsAsync([Story("1", "Story A")]);
		var sut = new BacklogService(gateway.Object);
		await sut.Connect();

		var diff = await sut.Refresh();

		Assert.False(diff.HasChanges);
		Assert.Empty(diff.Added);
		Assert.Empty(diff.Removed);
		Assert.Empty(diff.Changed);
	}

	private static Mock<IBacklogGateway> CreateGatewayMock(IReadOnlyList<UserStory> stories)
	{
		var mock = new Mock<IBacklogGateway>(MockBehavior.Strict);
		mock.Setup(gateway => gateway.ReadStories()).ReturnsAsync(stories);
		return mock;
	}

	private static UserStory Story(
		string id,
		string title,
		string? description = "Description",
		string? acceptanceCriteria = "Acceptance",
		int? priority = 1,
		string? status = "New",
		IReadOnlyList<string>? labels = null) =>
		new()
		{
			Id = id,
			Title = title,
			Description = description,
			AcceptanceCriteria = acceptanceCriteria,
			Priority = priority,
			Status = status,
			Labels = labels ?? ["Tag"]
		};
}