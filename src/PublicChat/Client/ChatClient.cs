using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PublicChat.Client;

public sealed record ChatMessage(string Role, string Content);

public sealed record ChatResult(string Text, string? ResponseId);

public sealed class ChatClient(
    IConfiguration configuration,
    IAccessTokenProvider tokenProvider,
    NavigationManager navigation)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ChatResult> SendAsync(IReadOnlyList<ChatMessage> messages)
    {
        if (messages.Count == 0)
        {
            throw new ArgumentException("At least one chat message is required.", nameof(messages));
        }

        var functionScope = Required("AzureAd:FunctionScope");
        var oboScope = Required("AzureAd:OboScope");
        var endpoint = Required("ChatApi:Endpoint");

        var functionToken = await tokenProvider.RequestAccessToken(
            new AccessTokenRequestOptions { Scopes = [functionScope] })
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(30));
        var oboToken = await tokenProvider.RequestAccessToken(
            new AccessTokenRequestOptions { Scopes = [oboScope] })
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(30));

        if (!functionToken.TryGetToken(out var functionAccessToken))
        {
            throw new AccessTokenNotAvailableException(navigation, functionToken, [functionScope]);
        }

        if (!oboToken.TryGetToken(out var oboAccessToken))
        {
            throw new AccessTokenNotAvailableException(navigation, oboToken, [oboScope]);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new { messages })
        };
        request.Headers.Authorization = new("Bearer", functionAccessToken.Value);
        request.Headers.Add("x-client-user-token", oboAccessToken.Value);

        using var client = new HttpClient();
        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var relayError = JsonSerializer.Deserialize<RelayError>(content, JsonOptions)?.Error;
            throw new HttpRequestException(
                relayError ?? "The chat request could not be completed. Please try again.",
                null,
                response.StatusCode);
        }

        var foundryResponse = JsonSerializer.Deserialize<FoundryResponse>(content, JsonOptions)
            ?? throw new InvalidOperationException("The agent returned an empty response.");

        if (!string.Equals(foundryResponse.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                foundryResponse.Error?.Message ?? "The agent could not complete the response.");
        }

        var text = foundryResponse.Output
            .SelectMany(item => item.Content ?? [])
            .Where(item => string.Equals(item.Type, "output_text", StringComparison.Ordinal))
            .Select(item => item.Text)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return new ChatResult(
            text ?? "The agent completed the request without a text response.",
            foundryResponse.Id);
    }

    private string Required(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key} must be configured.");

    private sealed record RelayError(string Error);

    private sealed record FoundryResponse(
        string? Id,
        string Status,
        IReadOnlyList<FoundryOutput> Output,
        FoundryError? Error);

    private sealed record FoundryOutput(IReadOnlyList<FoundryContent>? Content);

    private sealed record FoundryContent(string Type, string Text);

    private sealed record FoundryError(string Message);
}
