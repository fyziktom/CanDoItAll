# Symbol and reference map

## Existing symbols to inspect before edits

| Symbol | Why |
| --- | --- |
| `CanDoItAll.Modules.Workspace.ProviderProfile` | canonical EF provider row |
| `WorkspaceService.SaveProviderAsync` | profile validation/commit conventions |
| `IWorkspaceProviderProfileCommitObserver` | projection/cache invalidation |
| `ProviderRegistry` | connector manifest registry |
| `IProviderAdapter` | basic Workspace provider behavior |
| `ProviderConnectorFieldKeys` | current schema/config keys |
| `WorkspaceBackedAgentProviderProfileRegistry` | canonical runtime projection |
| `WorkspaceAgentProviderProfileMapper` | connector -> MAF effective profile |
| `AgentFrameworkProviderMetadata` | versioned provider metadata |
| `IProviderProfileService` | profile normalization/feature matrix |
| `MafProviderAgentFactory` | ordinary agent runtime path |
| `MafProviderRuntimeGateway` | health/test/image runtime path |
| `AgentProviderDriverRegistry` | provider capability registration |
| `ISecretRuntimeResolver` | secret lookup |
| `ApiAccessScopeNames` | scope constants |
| `ApiAuthorizationPolicies` | route policy names |
| `ApplyApiAuthorization` | existing optional-auth route convention |
| `ApiEndpointResults` | native error mapping |
| `AppDbContextModelRegistry` | module EF configurations |
| `IProviderUsageProjectionSource` | usage extension direction |
| `ProviderUsageCompleteness` | missing usage semantics |

## New conceptual symbols

Names are preferred, not mandatory:

- `SharedProviderProtocolVersions`
- `SharedProviderApiRoutes`
- `SharedProviderCapability`
- `SharedProviderCatalogResponse`
- `SharedProviderCatalogEntry`
- `SharedProviderCatalogModel`
- `SharedProviderRoutingModelId`
- `ISharedProviderCatalogClient`
- `ISharedProviderInferenceTransport`
- `ISharedProviderUpstreamAdapter`
- `SharedProviderUpstreamAdapterRegistry`
- `ProviderSharePublication`
- `SharedProviderSource`
- `SharedProviderImport`
- `SharedProviderInvocationRecord`
- `SharedProviderPublicationService`
- `SharedProviderCatalogService`
- `SharedProviderSourceService`
- `SharedProviderReconciliationService`
- `SharedProviderRuntimeProfileMaterializer`
- `SharedProviderInvocationService`
- `SharedProviderSourceUriPolicy`
- `AccessContextReference`
- `IAccessContextReferenceAccessor`
- `SharedProviderConnectorAdapter`
- `SharedProviderApi`

## Reference checks

Before closure, static/CodeAnalytics proof must show:

- no public `SharedProvider*` protocol record contains Workspace `ProviderProfile`;
- no Abstractions project reference to Workspace/Web/EF/MAF SDK;
- no Http reference to Razor/Web/EF entities;
- no MAF/Core reference to Workspace or Http;
- no component direct reference to `HttpClient` for source sync;
- no API endpoint direct reference to `DbContext`;
- no secret value property on publication/catalog/import DTOs;
- one adapter registry rather than repeated connector switches in endpoints.
