# CanDoItAll.AgentFramework.ProviderHistory.Abstractions

Defines provider-history identities, invocation and attempt evidence, capture policies, authorization/query contracts, and source ports. These contracts are independent of EF Core, Blazor, and provider SDKs. Logical request contexts carry retry evidence and the original input capture deadline.

Use the repository-pinned .NET SDK and the sibling source dependencies described in the [root README](../../../../README.md). Run these commands from the repository root:

```powershell
dotnet build ./src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Abstractions/CanDoItAll.AgentFramework.ProviderHistory.Abstractions.csproj --configuration Release /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx --configuration Release --filter "FullyQualifiedName~ProviderHistoryLifecycleTests" /m:1
```

See [shared providers](../../../../docs/shared-providers.md), [request history](../../../../docs/provider-request-history.md), [architecture](../../../../docs/architecture/overview.md), and [testing](../../../../docs/testing.md).
