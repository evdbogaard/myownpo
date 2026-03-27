using Microsoft.Extensions.Options;

using Moq;

using MyOwnPo.Gateways;

using Xunit;

namespace MyOwnPo.UnitTests;

public class AzureDevOpsBacklogGatewayTests
{
	[Fact]
	public async Task ReadStories_ValidResponse_ReturnsAllStories()
	{
		var settings = CreateSettings();
		var client = new Mock<IWorkItemTrackingClient>(MockBehavior.Strict);
		client.Setup(mock => mock.ReadUserStories(default))
			.ReturnsAsync([
				StoryFields("1", "Story A"),
				StoryFields("2", "Story B")
			]);
		var sut = new AzureDevOpsBacklogGateway(Options.Create(settings), client.Object);

		var result = await sut.ReadStories();

		Assert.Equal(2, result.Count);
	}

	[Fact]
	public async Task ReadStories_InvalidPat_ThrowsAuthException()
	{
		var settings = CreateSettings();
		var client = new Mock<IWorkItemTrackingClient>(MockBehavior.Strict);
		client.Setup(mock => mock.ReadUserStories(default))
			.ThrowsAsync(new Exception("401 Unauthorized"));
		var sut = new AzureDevOpsBacklogGateway(Options.Create(settings), client.Object);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ReadStories());
		Assert.Contains("authentication failed", exception.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task ReadStories_ProjectNotFound_ThrowsDescriptiveException()
	{
		var settings = CreateSettings(projectName: "UnknownProject");
		var client = new Mock<IWorkItemTrackingClient>(MockBehavior.Strict);
		client.Setup(mock => mock.ReadUserStories(default))
			.ThrowsAsync(new Exception("TF200016: The following project does not exist"));
		var sut = new AzureDevOpsBacklogGateway(Options.Create(settings), client.Object);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ReadStories());
		Assert.Contains("UnknownProject", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ReadStories_EmptyBacklog_ReturnsEmptyList()
	{
		var settings = CreateSettings();
		var client = new Mock<IWorkItemTrackingClient>(MockBehavior.Strict);
		client.Setup(mock => mock.ReadUserStories(default))
			.ReturnsAsync(new List<IDictionary<string, object?>>());
		var sut = new AzureDevOpsBacklogGateway(Options.Create(settings), client.Object);

		var result = await sut.ReadStories();

		Assert.Empty(result);
	}

	[Fact]
	public async Task ReadStories_WorkItemFieldMapping_MapsAllFieldsCorrectly()
	{
		var settings = CreateSettings();
		var client = new Mock<IWorkItemTrackingClient>(MockBehavior.Strict);
		client.Setup(mock => mock.ReadUserStories(default))
			.ReturnsAsync([
				new Dictionary<string, object?>
				{
					["System.Id"] = 7,
					["System.Title"] = "Mapped story",
					["System.Description"] = "<p>Hello&nbsp;world</p>",
					["Microsoft.VSTS.Common.AcceptanceCriteria"] = "<ul><li>A</li></ul>",
					["Microsoft.VSTS.Common.Priority"] = 2,
					["System.State"] = "Active",
					["System.Tags"] = "one; two; one"
				}
			]);
		var sut = new AzureDevOpsBacklogGateway(Options.Create(settings), client.Object);

		var result = await sut.ReadStories();

		Assert.Single(result);
		var story = result[0];
		Assert.Equal("7", story.Id);
		Assert.Equal("Mapped story", story.Title);
		Assert.Equal("Hello world", story.Description);
		Assert.Equal("A", story.AcceptanceCriteria);
		Assert.Equal(2, story.Priority);
		Assert.Equal("Active", story.Status);
		Assert.Equal(2, story.Labels.Count);
	}

	private static AzureDevOpsSettings CreateSettings(
		string organizationUrl = "https://dev.azure.com/example",
		string projectName = "Project",
		string areaPath = "",
		string pat = "pat") =>
		new()
		{
			OrganizationUrl = organizationUrl,
			ProjectName = projectName,
			AreaPath = areaPath,
			Pat = pat
		};

	private static IDictionary<string, object?> StoryFields(string id, string title) =>
		new Dictionary<string, object?>
		{
			["System.Id"] = id,
			["System.Title"] = title,
			["System.Description"] = "Description",
			["Microsoft.VSTS.Common.AcceptanceCriteria"] = "Acceptance",
			["Microsoft.VSTS.Common.Priority"] = 1,
			["System.State"] = "New",
			["System.Tags"] = "Tag"
		};
}