# Public WASM Chat Client Deployment Plan

## Status
Deployed

## Scope
Create a public .NET Blazor WebAssembly chat client protected by Microsoft Entra ID. The client will acquire a delegated token for the existing OBO API application and submit it only to a server-side relay. The relay will forward it as `x-client-user-token` to the Foundry hosted agent.

## Mode and Recipe
- Mode: MODIFY. The workspace contains the existing .NET hosted agent and its Foundry deployment manifest.
- Recipe: a separate Azure Developer CLI and Bicep deployment unit for the public client and relay. It will not alter the existing Foundry `azure.yaml`.
- Function trigger: HTTP.

## Selected Architecture
```text
Public browser
  -> Azure Static Web Apps Free: Blazor WebAssembly client
  -> Azure Functions Flex Consumption: authenticated chat relay
  -> Private VNet integration
  -> Microsoft Foundry private project and hosted Graph OBO agent
  -> Microsoft Graph (delegated OBO)
```

- Public Blazor WebAssembly client with Microsoft Entra ID authentication.
- Azure Static Web Apps Free hosts the client at its default Azure-provided URL.
- Azure Functions Flex Consumption provides the public, Entra-protected HTTP relay and has VNet integration to access the private Foundry project.
- The Function uses a dedicated user-assigned managed identity for its runtime storage and Foundry data-plane authentication, and forwards the user's OBO API assertion only as `x-client-user-token`.
- The Function never logs, persists, or includes the assertion in prompts, model inputs, responses, telemetry, or errors.
- The private Foundry hosted agent keeps its Key Vault-based confidential-client OBO flow.
- Azure API Management is explicitly out of scope.

## Decisions Pending
- None. The default Static Web Apps hostname is sufficient for this POC.

## Requirements
- Classification: POC.
- Scale: a small number of prototype users.
- Budget: cost-optimized; consumption-based cold starts are acceptable.
- Public hosting: Azure Static Web Apps Free.
- Subscription: `b651dacd-e6f5-465b-a17c-25f3a2cdd0c8`.
- Resource group and region: `rg-fdr-obo-private-gerwc`, Germany West Central.
- Identity boundary: tenant users only.
- Identity registrations: dedicated SPA and Function API registrations; the existing test public client remains unchanged.

## Infrastructure
- Add a dedicated Function subnet in `vnet-fdr-obo-private-gerwc`; it will not reuse the delegated `snet-agent` subnet or the private-endpoint subnet.
- Deploy a Flex Consumption Function App (FC1), a dedicated private runtime storage account, Application Insights, and Log Analytics.
- Use a dedicated user-assigned managed identity and RBAC for the Function runtime/storage and Foundry data-plane access. No application credentials or connection strings.
- Deploy Azure Static Web Apps Free with its own Entra application registration and an API client registration for the Function.
- Grant the SPA delegated access to the Function API scope. The Function validates issuer, tenant, audience, scopes, and bearer-token signature.
- Configure CORS only for the Static Web Apps origin and require HTTPS.

## Implementation
1. Scaffold a .NET Blazor WebAssembly client with MSAL and tenant-only sign-in.
2. Scaffold a .NET isolated HTTP Azure Function relay, preserving authenticated header handling and explicit error responses.
3. Add the Function's Foundry managed-identity request and the caller-token forwarding logic.
4. Generate the Function/template-derived Bicep resources, RBAC, delegated subnet, Static Web Apps configuration, and application settings.
5. Add concise operational and architecture documentation, reconciling the current README with the final Standard-private agent plus public-client topology.

## Validation and Deployment
1. Build the WASM client, Function, and existing hosted agent.
2. Validate the generated Azure configuration and IaC.
3. Deploy the public client and relay only after Azure validation.
4. Verify the public site, tenant-only sign-in, Function JWT validation, CORS, private DNS/network access to Foundry, and a OneDrive OBO chat response.

## Policy Constraints
- Current subscription-level policy assignments do not reveal a deployment-blocking constraint for this design.
- Existing subscription policy requires private Key Vault access; the architecture preserves the existing private Key Vault and Foundry VNet boundary.

## Validation Proof
- 2026-08-07 UX/multi-turn deployment: deployed the updated Function relay and Blazor client with `azd deploy --no-prompt`, then deployed hosted agent version `3` from the private VM.
- 2026-08-07 UX/multi-turn live test: verified a two-turn authenticated browser conversation. The first response rendered human-readable MB/KB values; the follow-up used prior context to compare the largest folders.
- 2026-08-07 UX/multi-turn operational checks: SPA callback `200`, unauthenticated relay `401`, CORS preflight `204`, and relay identity retains Foundry Agent Consumer.
- 2026-08-07 UX/multi-turn update: `dotnet build` succeeded for the Blazor client, Function relay, and hosted agent with zero warnings and errors.
- 2026-08-07 UX/multi-turn update: `azd provision --preview --no-prompt` completed successfully from `src\PublicChat\Relay`; no infrastructure changes are required for the application-only deployment.
- 2026-08-07 UX/multi-turn update: `az bicep build --file src\PublicChat\Relay\infra\main.bicep` succeeded with only the existing non-blocking template warnings.
- 2026-08-07 UX/multi-turn update: static RBAC verification confirmed the relay assignment still uses current Foundry Agent Consumer role ID `eed3b665-ab3a-47b6-8f48-c9382fb1dad6`.
- `dotnet build src\PublicChat\Client\PublicChat.Client.csproj --no-restore` succeeded.
- `dotnet build src\PublicChat\Relay\GraphOboRelay\GraphOboRelay.csproj --no-restore` succeeded.
- `az bicep build --file src\PublicChat\Relay\infra\main.bicep` succeeded.
- `azd provision --preview --no-prompt` in `src\PublicChat\Relay` succeeded. It creates only the public relay, its private runtime dependencies, and the Static Web App while reusing the private Foundry foundation.
- `azd package --no-prompt` in `src\PublicChat\Relay` succeeded for both the Function and Blazor WASM services.
- Static RBAC verification confirmed the current `Foundry Agent Consumer` role ID `eed3b665-ab3a-47b6-8f48-c9382fb1dad6`, scoped to the Foundry account.
- Deployed the Function relay at `https://func-api-mltnqc4mt47uc.azurewebsites.net/api/chat` and the Blazor WASM client at `https://thankful-smoke-023f1f703.7.azurestaticapps.net/`.
- Confirmed the WASM client redirects to tenant Microsoft Entra sign-in, and the relay returns `401` without a bearer token.
- The earlier diagnostic deployment of hosted agent version `2` passed its private VNet smoke test; the current deployed version is `3` as recorded above.
- Verified the hosted runtime authenticates to Key Vault with the Foundry project's system-assigned managed identity. Granted that identity Key Vault Secrets User on `kv-fdr-obo-5mgq`.
- Reconciled the OBO application registration with delegated Microsoft Graph `Files.Read` and granted tenant-wide admin consent to the OBO enterprise application.
- Completed the final public browser test. Response `caresp_c113f181386adad800buwQQqAHoFBLmkl4bs6gHXPUcYsPthPl` returned status `completed` and listed only the signed-in user's OneDrive root folders and sizes.
