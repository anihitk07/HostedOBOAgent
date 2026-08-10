using Azure.AI.AgentServer.Responses;
using Azure.AI.AgentServer.Responses.Models;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using DotNetEnv;
using HostedOBOAgent;
using Microsoft.Extensions.Logging;
using OpenAI.Responses;

Env.NoClobber().TraversePath().Load();

ResponsesServer.Run<GraphOboResponseHandler>(configure: builder =>
{
    var configuration = GraphOboOptions.FromEnvironment();
    var credential = new DefaultAzureCredential();
    var projectClient = new AIProjectClient(new Uri(configuration.FoundryProjectEndpoint), credential);

    builder.Services.AddSingleton(configuration);
    builder.Services.AddSingleton(projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel(configuration.ModelDeploymentName));
    builder.Services.AddSingleton(new SecretClient(new Uri(configuration.KeyVaultUrl), credential));
    builder.Services.AddHttpClient<GraphClient>();
    builder.Services.AddSingleton<OboTokenProvider>();
});

public sealed class GraphOboResponseHandler(
    ProjectResponsesClient responsesClient,
    GraphOboOptions options,
    GraphClient graphClient,
    ILogger<GraphOboResponseHandler> logger) : ResponseHandler
{
    private const string SystemPrompt = """
        You are a concise assistant that helps users understand their OneDrive.
        Use the supplied OneDrive data only when it is present, and never claim access
        to data that was not supplied. If no OneDrive data is present, explain that a
        signed-in user assertion is required.
        Format responses for people, not machines. Use short headings and lists where
        helpful. Convert byte sizes to appropriate KB, MB, or GB values, rounded to one
        decimal place, and include raw bytes only when the user explicitly requests them.
        """;

    public override IAsyncEnumerable<ResponseStreamEvent> CreateAsync(
        CreateResponse request,
        ResponseContext context,
        CancellationToken cancellationToken)
    {
        return new TextResponse(context, request, createText: ct => GenerateTextAsync(context, ct));
    }

    private async Task<string> GenerateTextAsync(ResponseContext context, CancellationToken cancellationToken)
    {
        var userInput = await context.GetInputTextAsync(cancellationToken: cancellationToken) ?? "Hello!";
        var userAssertion = context.ClientHeaders.TryGetValue(options.UserTokenHeaderName, out var assertion)
            ? assertion
            : null;

        var instructions = SystemPrompt;
        if (RequiresOneDriveData(userInput))
        {
            if (string.IsNullOrWhiteSpace(userAssertion))
            {
                instructions += "\nThe request needs OneDrive data, but no delegated user assertion was supplied.";
            }
            else
            {
                logger.LogInformation("Retrieving delegated OneDrive data for response {ResponseId}", context.ResponseId);
                var folders = await graphClient.GetRootFoldersAsync(userAssertion, cancellationToken);
                instructions += $"\nOneDrive root folders for the signed-in user:\n{folders}";
            }
        }

        var responseOptions = new CreateResponseOptions { Instructions = instructions };
        foreach (var item in await context.GetHistoryAsync(cancellationToken))
        {
            if (item is not OutputItemMessage { Content: { } contents })
            {
                continue;
            }

            foreach (var content in contents)
            {
                switch (content)
                {
                    case MessageContentOutputTextContent { Text: { } assistantText }:
                        responseOptions.InputItems.Add(ResponseItem.CreateAssistantMessageItem(assistantText));
                        break;
                    case MessageContentInputTextContent { Text: { } priorUserText }:
                        responseOptions.InputItems.Add(ResponseItem.CreateUserMessageItem(priorUserText));
                        break;
                }
            }
        }

        responseOptions.InputItems.Add(ResponseItem.CreateUserMessageItem(userInput));
        var response = await responsesClient.CreateResponseAsync(responseOptions, cancellationToken);
        return response.Value.GetOutputText() ?? "I could not generate a response.";
    }

    private static bool RequiresOneDriveData(string input) =>
        input.Contains("onedrive", StringComparison.OrdinalIgnoreCase) ||
        input.Contains("folder", StringComparison.OrdinalIgnoreCase) ||
        input.Contains("file", StringComparison.OrdinalIgnoreCase);
}
