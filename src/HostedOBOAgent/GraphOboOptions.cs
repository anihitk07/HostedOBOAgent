namespace HostedOBOAgent;

public sealed record GraphOboOptions(
    string FoundryProjectEndpoint,
    string ModelDeploymentName,
    string KeyVaultUrl,
    string OboTenantId,
    string OboClientId,
    string OboClientSecretName,
    string UserTokenHeaderName)
{
    public static GraphOboOptions FromEnvironment() => new(
        Required("FOUNDRY_PROJECT_ENDPOINT"),
        Required("AZURE_AI_MODEL_DEPLOYMENT_NAME"),
        Required("KEY_VAULT_URL"),
        Required("APP_OBO_TENANT_ID"),
        Required("APP_OBO_CLIENT_ID"),
        Required("APP_OBO_CLIENT_SECRET_NAME"),
        Environment.GetEnvironmentVariable("CLIENT_USER_TOKEN_HEADER") ?? "x-client-user-token");

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"{name} environment variable is required.");
}
