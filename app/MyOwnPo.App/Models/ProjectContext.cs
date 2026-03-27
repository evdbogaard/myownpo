namespace MyOwnPo.Models;

public class ProjectContext
{
	public string? Vision { get; set; }
	public string? BusinessGoals { get; set; }
	public string? TargetUsers { get; set; }
	public string? SprintFocus { get; set; }
	public string? Constraints { get; set; }

	public bool IsEmpty =>
		string.IsNullOrWhiteSpace(Vision)
		&& string.IsNullOrWhiteSpace(BusinessGoals)
		&& string.IsNullOrWhiteSpace(TargetUsers)
		&& string.IsNullOrWhiteSpace(SprintFocus)
		&& string.IsNullOrWhiteSpace(Constraints);
}