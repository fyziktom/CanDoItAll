# CanDoItAll.AgentFramework.ProviderHistory.Application

Owns history query validation, authorization orchestration, bounded text redaction, completion transitions, and source coordination. It depends on history abstractions; encryption, SQL, HTTP, and UI remain outside this project.

Use the repository-pinned .NET SDK and the sibling source dependencies described in the [root README](../../../../README.md). Run these commands from the repository root:

```powershell
dotnet build ./src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Application/CanDoItAll.AgentFramework.ProviderHistory.Application.csproj --configuration Release /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx --configuration Release --filter "FullyQualifiedName~ProviderHistoryLifecycleTests" /m:1
```

See [shared providers](../../../../docs/shared-providers.md), [request history](../../../../docs/provider-request-history.md), [architecture](../../../../docs/architecture/overview.md), and [testing](../../../../docs/testing.md).
