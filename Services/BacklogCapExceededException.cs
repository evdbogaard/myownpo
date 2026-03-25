namespace MyOwnPo.Services;

public class BacklogCapExceededException : Exception
{
	public BacklogCapExceededException(int storyCount)
		: base($"Backlog contains {storyCount} stories, which exceeds the maximum supported size of 100.")
	{
		StoryCount = storyCount;
	}

	public int StoryCount { get; }
}