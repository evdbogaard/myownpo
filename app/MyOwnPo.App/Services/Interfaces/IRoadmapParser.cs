using MyOwnPo.Models;

namespace MyOwnPo.Services.Interfaces;

public interface IRoadmapParser
{
	IReadOnlyList<RoadmapItem> Parse(string markdown);
}