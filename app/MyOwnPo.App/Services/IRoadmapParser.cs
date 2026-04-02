using MyOwnPo.Models;

namespace MyOwnPo.Services;

public interface IRoadmapParser
{
	IReadOnlyList<RoadmapItem> Parse(string markdown);
}