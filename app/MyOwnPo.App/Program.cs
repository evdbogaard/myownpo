using System.ClientModel;

using Azure.AI.OpenAI;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using MyOwnPo;
using MyOwnPo.Gateways;
using MyOwnPo.Services;
using MyOwnPo.Services.Interfaces;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
	.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
	.AddUserSecrets<Program>(optional: true);

builder.Services
	.AddOptions<AzureDevOpsSettings>()
	.Bind(builder.Configuration.GetSection("AzureDevOps"))
	.ValidateDataAnnotations()
	.ValidateOnStart();

builder.Services
	.AddOptions<AzureOpenAiSettings>()
	.Bind(builder.Configuration.GetSection("AzureOpenAi"))
	.ValidateDataAnnotations()
	.ValidateOnStart();

builder.Services.AddSingleton<IWorkItemTrackingClient, AzureDevOpsWorkItemTrackingClient>();
builder.Services.AddSingleton<IBacklogGateway, AzureDevOpsBacklogGateway>();
builder.Services.AddSingleton<IBacklogService, BacklogService>();

builder.Services.AddSingleton<IChatClient>(serviceProvider =>
{
	var settings = serviceProvider.GetRequiredService<IOptions<AzureOpenAiSettings>>().Value;
	var azureClient = new AzureOpenAIClient(
		new Uri(settings.Endpoint),
		new ApiKeyCredential(settings.ApiKey));

	return azureClient
		.GetChatClient(settings.DeploymentName)
		.AsIChatClient()
		.AsBuilder()
		.UseFunctionInvocation()
		.Build();
});

builder.Services.AddSingleton<IContextFileStore>(new JsonContextFileStore("project-context.json"));
builder.Services.AddSingleton<IProjectContextService, ProjectContextService>();
builder.Services.AddSingleton<IRoadmapFileLoader, RoadmapMarkdownFileLoader>();
builder.Services.AddSingleton<IRoadmapParser, RoadmapMarkdownParser>();
builder.Services.AddSingleton<IProductOwnerBrainService, ProductOwnerBrainService>();
builder.Services.AddSingleton<ConsoleHost>();

using var host = builder.Build();
var consoleHost = host.Services.GetRequiredService<ConsoleHost>();
await consoleHost.Run();