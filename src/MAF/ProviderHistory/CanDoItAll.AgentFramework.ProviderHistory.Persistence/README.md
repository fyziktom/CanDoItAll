# CanDoItAll.AgentFramework.ProviderHistory.Persistence

Implements PostgreSQL history capture, protected details, quota accounting, search, leases, source/outbox projection, retention, and database transfer. It depends on history contracts/application policy and Foundation infrastructure. Product migrations remain in the PostgreSQL migrations project.

Runtime stores use `ProviderHistoryDbContext` with eleven explicit owner mappings. The complete application schema retains the same configurations and remains the migration authority. GUID stamping for entries, policies, checkpoints, and sources uses the same `ApplicationManagedConcurrencyTokens` implementation as the complete schema. No table, identity, conversion, constraint, or migration changes are introduced by this boundary.

The standalone pooled factory is pinned to `ICanonicalRuntimeDatabase.Profile`. Partition reads, capture, policy operations, projection processing, retention maintenance, and verification use that independent factory. Ordinary factory calls never join an ambient transaction.

Cross-owner writes require the caller's infrastructure to enter `CoordinatedDatabaseTransaction` after acquiring its transaction. Partition `GetForWriteAsync` / `RequireForWriteAsync`, outbox `StageAsync`, projection `StageAsync`, and metadata-retention resolution use fresh enlisted contexts and save before returning. They neither commit nor own the caller's connection or transaction. `RequireAsync` remains an explicitly standalone partition check. Public owner operations accept typed history values, while EF query/detail helpers remain internal.

`HistoryTargetWriteSession` is an explicit transfer composition helper. It builds its own coordinator, options, and history services from the resolved target profile. Transfer infrastructure enters `session.Transactions` with the existing target transaction and calls the session's typed operations; the runtime profile is never substituted for the target. The current complete-schema transfer handler, batch copier, and participant contract remain identified maintenance seams pending the coordinated transfer boundary. The shared-provider expiry join also remains a separate integration-query seam; its filter/order/limit semantics are unchanged by this context boundary.

PostgreSQL supplies the shared-transaction guarantee. The coordinator's explicit InMemory test mode cannot supply cross-context atomicity, and PostgreSQL-specific history projection/maintenance still requires PostgreSQL. Do not treat an InMemory run as transaction proof.

Use the repository-pinned .NET SDK and the sibling source dependencies described in the [root README](../../../../README.md). Run these commands from the repository root:

```powershell
dotnet build ./src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence/CanDoItAll.AgentFramework.ProviderHistory.Persistence.csproj --configuration Release /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Integration.slnx --configuration Release --filter "FullyQualifiedName~ProviderHistoryPersistenceIntegrationTests" /m:1
```

Persistence cases create disposable databases on the configured test PostgreSQL instance. Fake upstream tests do not require paid provider credentials. See the test guide before enabling any Docker or live-host lane.

`ProviderHistoryOwnerPersistenceTests` adds owner-model/schema parity, GUID stamping, complete-schema record readback after restart, and enlisted writer/target-profile coverage. Existing persistence, source-projection, capture, authorization, and transfer tests remain required. Confirm discovery before execution and run portability-static enforcement for source changes; this document does not assert a passing result.

See [shared providers](../../../../docs/shared-providers.md), [request history](../../../../docs/provider-request-history.md), [architecture](../../../../docs/architecture/overview.md), and [testing](../../../../docs/testing.md).
