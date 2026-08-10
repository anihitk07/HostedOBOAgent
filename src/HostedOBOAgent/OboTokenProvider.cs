using Azure.Security.KeyVault.Secrets;
using Microsoft.Identity.Client;

namespace HostedOBOAgent;

public sealed class OboTokenProvider(
    GraphOboOptions options,
    SecretClient secretClient,
    ILogger<OboTokenProvider> logger)
{
    private const string GraphScope = "https://graph.microsoft.com/.default";

    public async Task<string> AcquireGraphTokenAsync(string userAssertion, CancellationToken cancellationToken)
    {
        var secret = await secretClient.GetSecretAsync(options.OboClientSecretName, cancellationToken: cancellationToken);
        var application = ConfidentialClientApplicationBuilder
            .Create(options.OboClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, options.OboTenantId)
            .WithClientSecret(secret.Value.Value)
            .Build();

        AuthenticationResult result;
        try
        {
            result = await application
                .AcquireTokenOnBehalfOf([GraphScope], new UserAssertion(userAssertion))
                .ExecuteAsync(cancellationToken);
        }
        catch (MsalException exception)
        {
            logger.LogError(
                exception,
                "Microsoft Graph OBO token acquisition failed. ErrorCode={ErrorCode}, StatusCode={StatusCode}",
                exception.ErrorCode,
                exception is MsalServiceException serviceException ? serviceException.StatusCode : null);
            throw;
        }

        return result.AccessToken;
    }
}
