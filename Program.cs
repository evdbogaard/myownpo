using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MyOwnPo;
using MyOwnPo.Gateways;
using MyOwnPo.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
	.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
	.AddUserSecrets<Program>(optional: true);

builder.Services
	.AddOptions<AzureDevOpsSettings>()
	.Bind(builder.Configuration.GetSection("AzureDevOps"))
	.ValidateDataAnnotations()
	.ValidateOnStart();

builder.Services.AddSingleton<IWorkItemTrackingClient, AzureDevOpsWorkItemTrackingClient>();
builder.Services.AddSingleton<IBacklogGateway, AzureDevOpsBacklogGateway>();
builder.Services.AddSingleton<IBacklogService, BacklogService>();
builder.Services.AddSingleton<ConsoleHost>();

using var host = builder.Build();
var consoleHost = host.Services.GetRequiredService<ConsoleHost>();
await consoleHost.Run();