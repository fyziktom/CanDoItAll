# CanDoItAll.Modules.AgentFramework.ProviderManagement

Owns provider administration and its Blazor surfaces, publication/source/import lifecycle, catalog projection and routing, invocation audit, and history source integration. Protocol transport belongs to SharedProviders.Http; provider execution belongs to the provider/MAF runtime; Web and Composition own endpoint registration and wiring.

Use the repository-pinned .NET SDK and the sibling source dependencies described in the [root README](../../../README.md). Run these commands from the repository root:

```powershell
dotnet build ./src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/CanDoItAll.Modules.AgentFramework.ProviderManagement.csproj --configuration Release /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx --configuration Release --filter "FullyQualifiedName~SharedProviderPublicationAndCatalogTests" /m:1
```

See [shared providers](../../../docs/shared-providers.md), [request history](../../../docs/provider-request-history.md), [architecture](../../../docs/architecture/overview.md), and [testing](../../../docs/testing.md).
