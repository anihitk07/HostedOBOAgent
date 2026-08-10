using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace GraphOboRelay;

public sealed class ChatFunction(
    FoundryResponsesClient foundryClient,
    ILogger<ChatFunction> logger,
    IConfiguration configuration)
{
    [Function(nameof(ChatFunction))]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "chat")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (string.Equals(request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var preflight = request.CreateResponse(HttpStatusCode.NoContent);
            AddCorsHeaders(request, preflight);
            return preflight;
        }

        // App Service Authentication must validate the caller before the Function runs.
        if (!request.Headers.TryGetValues("X-MS-CLIENT-PRINCIPAL", out _))
        {
            return await CreateErrorAsync(request, HttpStatusCode.Unauthorized, "Authentication is required.");
        }

        if (!request.Headers.TryGetValues("x-client-user-token", out var userTokenValues) ||
            string.IsNullOrWhiteSpace(userTokenValues.SingleOrDefault()))
        {
            return await CreateErrorAsync(request, HttpStatusCode.BadRequest, "A delegated user token is required.");
        }

        ChatRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<ChatRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return await CreateErrorAsync(request, HttpStatusCode.BadRequest, "The request body must be valid JSON.");
        }

        if (body?.Messages is not { Count: > 0 } ||
            body.Messages.Count > 20 ||
            body.Messages.Any(message =>
                (message.Role is not "user" and not "assistant") ||
                string.IsNullOrWhiteSpace(message.Content) ||
                message.Content.Length > 8_000) ||
            body.Messages[^1].Role != "user")
        {
            return await CreateErrorAsync(
                request,
                HttpStatusCode.BadRequest,
                "Messages must contain 1-20 valid user/assistant turns, end with a user turn, and not exceed 8,000 characters each.");
        }

        try
        {
            var result = await foundryClient.SendAsync(body.Messages, userTokenValues.Single(), cancellationToken);
            var response = request.CreateResponse(HttpStatusCode.OK);
            AddCorsHeaders(request, response);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(result, cancellationToken);
            return response;
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                exception,
                "Foundry request failed with status code {StatusCode}.",
                exception.StatusCode);
            return await CreateErrorAsync(request, HttpStatusCode.BadGateway, "The agent is unavailable. Please try again.");
        }
    }

    private async Task<HttpResponseData> CreateErrorAsync(
        HttpRequestData request,
        HttpStatusCode statusCode,
        string message)
    {
        var response = request.CreateResponse(statusCode);
        AddCorsHeaders(request, response);
        await response.WriteAsJsonAsync(new { error = message });
        return response;
    }

    private void AddCorsHeaders(HttpRequestData request, HttpResponseData response)
    {
        var allowedOrigin = configuration["ALLOWED_ORIGIN"];
        if (string.IsNullOrWhiteSpace(allowedOrigin) ||
            !request.Headers.TryGetValues("Origin", out var origins) ||
            !string.Equals(origins.SingleOrDefault(), allowedOrigin, StringComparison.Ordinal))
        {
            return;
        }

        response.Headers.Add("Access-Control-Allow-Origin", allowedOrigin);
        response.Headers.Add("Access-Control-Allow-Headers", "authorization, content-type, x-client-user-token");
        response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
        response.Headers.Add("Access-Control-Max-Age", "600");
    }

    private sealed record ChatRequest(IReadOnlyList<ChatMessage> Messages);

    public sealed record ChatMessage(string Role, string Content);
}
