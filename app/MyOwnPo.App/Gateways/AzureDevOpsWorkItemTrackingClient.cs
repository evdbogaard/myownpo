using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;

namespace MyOwnPo.Gateways;

public class AzureDevOpsWorkItemTrackingClient(IOptions<AzureDevOpsSettings> settings) : IWorkItemTrackingClient
{
	private readonly AzureDevOpsSettings _settings = settings.Value;

	private static readonly string[] RequestedFields =
	[
		"System.Id",
		"System.Title",
		"System.Description",
		"Microsoft.VSTS.Common.AcceptanceCriteria",
		"Microsoft.VSTS.Common.Priority",
		"System.State",
		"System.Tags"
	];

	public async Task<IReadOnlyList<IDictionary<string, object?>>> ReadUserStories(
		CancellationToken cancellationToken = default)
	{
		var organizationUri = new Uri(_settings.OrganizationUrl, UriKind.Absolute);
		var credentials = new VssBasicCredential(string.Empty, _settings.Pat);

		using var client = new WorkItemTrackingHttpClient(organizationUri, credentials);
		var wiql = new Wiql { Query = BuildWiql(_settings.ProjectName, _settings.AreaPath) };

		var queryResult = await client.QueryByWiqlAsync(
			wiql,
			_settings.ProjectName,
			cancellationToken: cancellationToken);

		var storyIds = queryResult.WorkItems?.Select(reference => reference.Id).ToList() ?? [];
		if (storyIds.Count == 0)
			return [];

		var workItems = await client.GetWorkItemsAsync(
			storyIds,
			RequestedFields,
			cancellationToken: cancellationToken);

		return workItems
			.Select(ToFieldDictionary)
			.Cast<IDictionary<string, object?>>()
			.ToList();
	}

	private static Dictionary<string, object?> ToFieldDictionary(WorkItem workItem)
	{
		var fields = workItem.Fields is null
			? new Dictionary<string, object?>()
			: new Dictionary<string, object?>(workItem.Fields);

		fields["System.Id"] = workItem.Id;
		return fields;
	}

	private static string BuildWiql(string projectName, string? areaPath)
	{
		var escapedProject = EscapeWiqlString(projectName);
		var areaFilter = string.IsNullOrWhiteSpace(areaPath)
			? string.Empty
			: $" AND [System.AreaPath] UNDER '{EscapeWiqlString(areaPath)}'";

		return $"SELECT [System.Id] FROM WorkItems " +
			$"WHERE [System.TeamProject] = '{escapedProject}' " +
			"AND [System.WorkItemType] = 'User Story' " +
			"AND [System.State] NOT IN ('Closed', 'Removed')" +
			$"{areaFilter} ORDER BY [System.ChangedDate] DESC";
	}

	private static string EscapeWiqlString(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}