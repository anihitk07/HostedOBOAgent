using System.Net.Http.Headers;
using System.Text.Json;

namespace HostedOBOAgent;

public sealed class GraphClient(
    OboTokenProvider tokenProvider,
    HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> GetRootFoldersAsync(string userAssertion, CancellationToken cancellationToken)
    {
        var graphToken = await tokenProvider.AcquireGraphTokenAsync(userAssertion, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://graph.microsoft.com/v1.0/me/drive/root/children?$select=name,folder,size,lastModifiedDateTime");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", graphToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<GraphDriveItemCollection>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Microsoft Graph returned an empty response.");

        var folders = payload.Value
            .Where(item => item.Folder is not null)
            .OrderByDescending(item => item.Size ?? 0)
            .Select(item => new
            {
                item.Name,
                item.Size,
                item.LastModifiedDateTime,
                ChildCount = item.Folder?.ChildCount
            });

        return JsonSerializer.Serialize(folders, JsonOptions);
    }

    private sealed record GraphDriveItemCollection(IReadOnlyList<GraphDriveItem> Value);

    private sealed record GraphDriveItem(
        string Name,
        long? Size,
        DateTimeOffset? LastModifiedDateTime,
        GraphFolder? Folder);

    private sealed record GraphFolder(int? ChildCount);
}
