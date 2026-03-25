using System.ComponentModel.DataAnnotations;

namespace MyOwnPo.Gateways;

public class AzureDevOpsSettings
{
	[Required(AllowEmptyStrings = false)]
	[Url]
	[RegularExpression(@"^https://.+", ErrorMessage = "AzureDevOps:OrganizationUrl must be an HTTPS URL.")]
	public required string OrganizationUrl { get; init; }

	[Required(AllowEmptyStrings = false)]
	public required string ProjectName { get; init; }

	public string? AreaPath { get; init; }

	[Required(AllowEmptyStrings = false)]
	public required string Pat { get; init; }
}