namespace MyOwnPo.Services;

public class RoadmapMarkdownFileLoader : IRoadmapFileLoader
{
	public string Load(string filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath))
			throw new ArgumentException("Roadmap file path cannot be empty.", nameof(filePath));

		if (!File.Exists(filePath))
			throw new FileNotFoundException($"Roadmap file was not found at '{filePath}'.", filePath);

		return File.ReadAllText(filePath);
	}
}