using System.Globalization;
using System.Text.RegularExpressions;

using MyOwnPo.Models;
using MyOwnPo.Services.Interfaces;

namespace MyOwnPo.Services;

public partial class RoadmapMarkdownParser : IRoadmapParser
{
	private static readonly Regex BulletRegex = BulletLineRegex();
	private static readonly Regex HeadingRegex = HeadingLineRegex();
	private static readonly Regex NumberedListRegex = NumberedListLineRegex();
	private static readonly Regex TimeHorizonRegex = TimeHorizonLineRegex();

	public IReadOnlyList<RoadmapItem> Parse(string markdown)
	{
		if (string.IsNullOrWhiteSpace(markdown))
			return [];

		var roadmapItems = new List<RoadmapItem>();
		var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var rawLine in markdown.Split('\n'))
		{
			var line = rawLine.Trim();
			if (line.Length == 0)
				continue;

			var title = ExtractTitle(line);
			if (title is null)
				continue;

			var normalized = NormalizeWhitespace(title);
			if (normalized.Length == 0)
				continue;

			if (!seenTitles.Add(normalized))
				continue;

			roadmapItems.Add(new RoadmapItem
			{
				Id = $"roadmap-{roadmapItems.Count + 1:D3}",
				Title = normalized,
				TimeHorizon = ExtractTimeHorizon(normalized)
			});
		}

		return roadmapItems;
	}

	private static string? ExtractTitle(string line)
	{
		if (line.All(ch => ch == '-'))
			return null;

		var headingMatch = HeadingRegex.Match(line);
		if (headingMatch.Success)
			return StripInlineMarkdown(headingMatch.Groups[1].Value);

		var bulletMatch = BulletRegex.Match(line);
		if (bulletMatch.Success)
			return StripInlineMarkdown(bulletMatch.Groups[1].Value);

		var numberedMatch = NumberedListRegex.Match(line);
		if (numberedMatch.Success)
			return StripInlineMarkdown(numberedMatch.Groups[1].Value);

		return null;
	}

	private static string NormalizeWhitespace(string value) =>
		string.Join(' ', value
			.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

	private static string StripInlineMarkdown(string value)
	{
		var trimmed = value.Trim();
		trimmed = trimmed.Trim('*', '_', '`');
		return trimmed;
	}

	private static string? ExtractTimeHorizon(string value)
	{
		var match = TimeHorizonRegex.Match(value);
		if (!match.Success)
			return null;

		return CultureInfo.InvariantCulture.TextInfo.ToUpper(match.Value);
	}

	[GeneratedRegex("^[-*+]\\s+(.+)$", RegexOptions.Compiled)]
	private static partial Regex BulletLineRegex();

	[GeneratedRegex("^#{1,6}\\s+(.+)$", RegexOptions.Compiled)]
	private static partial Regex HeadingLineRegex();

	[GeneratedRegex("^\\d+[.)]\\s+(.+)$", RegexOptions.Compiled)]
	private static partial Regex NumberedListLineRegex();

	[GeneratedRegex("\\b(Q[1-4]\\s*\\d{4}|H[12]\\s*\\d{4}|FY\\d{2,4})\\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
	private static partial Regex TimeHorizonLineRegex();
}