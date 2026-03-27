using System.Net;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Options;

using MyOwnPo.Models;

namespace MyOwnPo.Gateways;

public class AzureDevOpsBacklogGateway(IOptions<AzureDevOpsSettings> settings, IWorkItemTrackingClient workItemTrackingClient) : IBacklogGateway
{
	private readonly AzureDevOpsSettings _settings = settings.Value;
	private readonly IWorkItemTrackingClient _workItemTrackingClient = workItemTrackingClient;

	public async Task<IReadOnlyList<UserStory>> ReadStories()
	{
		try
		{
			var workItems = await _workItemTrackingClient.ReadUserStories();
			return workItems.Select(MapToUserStory).ToList();
		}
		catch (Exception ex) when (IsAuthenticationFailure(ex))
		{
			throw new InvalidOperationException(
				"Azure DevOps authentication failed. Verify AzureDevOps:Pat is valid and has Work Items (Read) scope.",
				ex);
		}
		catch (Exception ex) when (IsProjectNotFound(ex))
		{
			throw new InvalidOperationException(
				$"Azure DevOps project '{_settings.ProjectName}' was not found or is inaccessible.",
				ex);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException(
				"Azure DevOps API is unreachable. Verify organization URL, network connectivity, and permissions.",
				ex);
		}
	}

	private static UserStory MapToUserStory(IDictionary<string, object?> fields)
	{
		var id = GetString(fields, "System.Id")
			?? throw new InvalidOperationException("A work item is missing System.Id.");
		var title = GetString(fields, "System.Title") ?? "[Untitled]";

		return new UserStory
		{
			Id = id,
			Title = title,
			Description = NormalizeHtml(GetString(fields, "System.Description")),
			AcceptanceCriteria = NormalizeHtml(GetString(fields, "Microsoft.VSTS.Common.AcceptanceCriteria")),
			Priority = GetInt(fields, "Microsoft.VSTS.Common.Priority"),
			Labels = ParseTags(GetString(fields, "System.Tags")),
			Status = GetString(fields, "System.State")
		};
	}

	private static bool IsAuthenticationFailure(Exception exception)
	{
		var message = exception.Message;
		return message.Contains("401", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("authentication", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("personal access token", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsProjectNotFound(Exception exception)
	{
		var message = exception.Message;
		return message.Contains("project", StringComparison.OrdinalIgnoreCase)
			&& message.Contains("not found", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("TF200016", StringComparison.OrdinalIgnoreCase);
	}

	private static string? GetString(IDictionary<string, object?> fields, string key)
	{
		if (!fields.TryGetValue(key, out var value) || value is null)
			return null;

		return value.ToString();
	}

	private static int? GetInt(IDictionary<string, object?> fields, string key)
	{
		if (!fields.TryGetValue(key, out var value) || value is null)
			return null;

		if (value is int intValue)
			return intValue;


		if (value is long longValue)
			return checked((int)longValue);


		if (int.TryParse(value.ToString(), out var parsed))
			return parsed;


		return null;
	}

	private static IReadOnlyList<string> ParseTags(string? tags)
	{
		if (string.IsNullOrWhiteSpace(tags))
			return [];


		return tags
			.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static string? NormalizeHtml(string? html)
	{
		if (string.IsNullOrWhiteSpace(html))
			return null;


		var withoutTags = Regex.Replace(html, "<.*?>", string.Empty);
		var decoded = WebUtility.HtmlDecode(withoutTags);
		var normalizedWhitespace = Regex.Replace(decoded, @"\s+", " ").Trim();
		return string.IsNullOrWhiteSpace(normalizedWhitespace) ? null : normalizedWhitespace;
	}
}