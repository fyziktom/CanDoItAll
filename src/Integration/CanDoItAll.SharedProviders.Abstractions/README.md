# CanDoItAll.SharedProviders.Abstractions

Defines the shared-provider wire protocol, typed identifiers, catalog contracts, relay requests/results, and adapter ports. It has no Web, EF Core, or provider-SDK dependency. Public HTTP and persistence implementations belong to their separate owners.

Use the repository-pinned .NET SDK and the sibling source dependencies described in the [root README](../../../README.md). Run these commands from the repository root:

```powershell
dotnet build ./src/Integration/CanDoItAll.SharedProviders.Abstractions/CanDoItAll.SharedProviders.Abstractions.csproj --configuration Release /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx --configuration Release --filter "FullyQualifiedName~SharedProviderProtocolContractTests" /m:1
```

See [shared providers](../../../docs/shared-providers.md), [request history](../../../docs/provider-request-history.md), [architecture](../../../docs/architecture/overview.md), and [testing](../../../docs/testing.md).
