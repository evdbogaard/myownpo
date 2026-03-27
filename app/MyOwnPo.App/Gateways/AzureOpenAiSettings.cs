using System.ComponentModel.DataAnnotations;

namespace MyOwnPo.Gateways;

public class AzureOpenAiSettings
{
    [Required(AllowEmptyStrings = false)]
    [Url]
    public required string Endpoint { get; init; }

    [Required(AllowEmptyStrings = false)]
    public required string DeploymentName { get; init; }

    [Required(AllowEmptyStrings = false)]
    public required string ApiKey { get; init; }
}