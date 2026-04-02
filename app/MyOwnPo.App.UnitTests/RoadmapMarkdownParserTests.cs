using MyOwnPo.Services;

using Xunit;

namespace MyOwnPo.UnitTests;

public class RoadmapMarkdownParserTests
{
	[Fact]
	public void Parse_RoadmapWithBulletsAndHeadings_ReturnsNormalizedItems()
	{
		const string markdown = """
			# Q2 2026 Priorities
			## Improve onboarding
			- Add self-serve onboarding checklist
			-   Expand user activation metrics
			1. Improve migration guidance
			----
			""";

		var sut = new RoadmapMarkdownParser();

		var items = sut.Parse(markdown);

		Assert.Equal(5, items.Count);
		Assert.Equal("roadmap-001", items[0].Id);
		Assert.Equal("Q2 2026 Priorities", items[0].Title);
		Assert.Equal("Q2 2026", items[0].TimeHorizon);
		Assert.Equal("Improve onboarding", items[1].Title);
		Assert.Equal("Add self-serve onboarding checklist", items[2].Title);
		Assert.Equal("Expand user activation metrics", items[3].Title);
		Assert.Equal("Improve migration guidance", items[4].Title);
	}

	[Fact]
	public void Parse_EmptyRoadmap_ReturnsNoItems()
	{
		var sut = new RoadmapMarkdownParser();

		var items = sut.Parse("   \n\n   ");

		Assert.Empty(items);
	}
}