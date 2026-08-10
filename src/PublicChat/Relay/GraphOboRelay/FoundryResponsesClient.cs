using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GraphOboRelay;

public sealed class FoundryResponsesClient(
    IConfiguration configuration,
    TokenCredential credential,
    HttpClient httpClient)
{
    private static readonly TokenRequestContext FoundryTokenRequest =
        new(["https://ai.azure.com/.default"]);

    public async Task<string> SendAsync(
        IReadOnlyList<ChatFunction.ChatMessage> messages,
        string userAssertion,
        CancellationToken cancellationToken)
    {
        var projectEndpoint = Required("FOUNDRY_PROJECT_ENDPOINT").TrimEnd('/');
        var agentName = Required("FOUNDRY_AGENT_NAME");
        var accessToken = await credential.GetTokenAsync(FoundryTokenRequest, cancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{projectEndpoint}/agents/{agentName}/endpoint/protocols/openai/responses?api-version=v1")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    input = messages.Select(message => new
                    {
                        role = message.Role,
                        content = message.Content
                    }),
                    stream = false
                }),
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        request.Headers.Add("x-client-user-token", userAssertion);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                "The Foundry Responses request failed.",
                null,
                response.StatusCode);
        }

        return responseBody;
    }

    private string Required(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key} must be configured.");
}
