namespace MyOwnPo.Models;

public class BacklogConnection
{
	public bool IsConnected { get; set; }
	public DateTimeOffset? LastRefreshed { get; set; }
	public int StoryCount { get; set; }
}