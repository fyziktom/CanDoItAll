# CanDoItAll.AgentFramework.ProviderHistory.Persistence

Implements PostgreSQL history capture, protected details, quota accounting, search, leases, source/outbox projection, retention, and database transfer. It depends on history contracts/application policy and Foundation infrastructure. Product migrations remain in the PostgreSQL migrations project.

Use the repository-pinned .NET SDK and the sibling source dependencies described in the [root README](../../../../README.md). Run these commands from the repository root:

```powershell
dotnet build ./src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence/CanDoItAll.AgentFramework.ProviderHistory.Persistence.csproj --configuration Release /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Integration.slnx --configuration Release --filter "FullyQualifiedName~ProviderHistoryPersistenceIntegrationTests" /m:1
```

Persistence cases create disposable databases on the configured test PostgreSQL instance. Fake upstream tests do not require paid provider credentials. See the test guide before enabling any Docker or live-host lane.

See [shared providers](../../../../docs/shared-providers.md), [request history](../../../../docs/provider-request-history.md), [architecture](../../../../docs/architecture/overview.md), and [testing](../../../../docs/testing.md).
