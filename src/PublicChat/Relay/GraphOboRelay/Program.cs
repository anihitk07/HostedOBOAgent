using Azure.Identity;
using Azure.Core;
using GraphOboRelay;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddSingleton<TokenCredential>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var clientId = configuration["AZURE_CLIENT_ID"]
                ?? throw new InvalidOperationException("AZURE_CLIENT_ID must be configured.");

            return new DefaultAzureCredential(
                new DefaultAzureCredentialOptions { ManagedIdentityClientId = clientId });
        });
        services.AddHttpClient<FoundryResponsesClient>();
    })
    .Build();

host.Run();
