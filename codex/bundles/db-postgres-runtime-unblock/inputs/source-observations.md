# Source observations

Reviewed branch `db-remove-sqlite` against `development` using GitHub connector and fetched representative files.

## Observed fulfilled items

- `src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs`
  - `DatabaseProviderKind` now contains only `PostgreSql = 1` and `InMemory = 2`.
  - `DatabaseProfileSourceKind` now contains only `PostgresConnection = 3` and `InMemory = 6`.
  - Retired SQLite connection/editor model fields were removed.

- `src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs`
  - No `UseSqlite`.
  - Default design-time provider is `PostgreSql`.

- `src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs`
  - Removed in branch diff.

- `src/CanDoItAll.Infrastructure/ControlPlane/LegacyDatabaseProfileCatalogQuarantine.cs`
  - Added raw JSON quarantine before typed profile deserialization.
  - Legacy profiles are removed from catalog and backed up.

- `src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor`
  - UI no longer exposes SQLite or snapshot controls.

- `codex/bundles/db-remove-sqlite-followup-bundle-v1/reviews/01-execution-report.md`
  - Claims restore, build, unit tests, targeted component tests, integration tests, Playwright Data Sources tests, residue audit, and bundle validator passed.
  - Also records residual risks: full component test project timed out after unrelated failures; a self-hosted startup-flow Playwright test is quarantined; build still has existing EF Core Relational warnings.

## Observed remaining risks

- Branch compare reported `db-remove-sqlite` is diverged from `development` (`ahead_by: 4`, `behind_by: 1`). Rebase/merge must happen before final merge proof.
- Existing `.codex/bundles/project-structure-workflow-runs/proof/...` artifacts and prior prepared bundles were added in the feature branch. Decide whether they should be retained or removed from the merge branch.
- `LegacyDatabaseProfileCatalogQuarantine.cs` uses `"Sql" + "ite"` and similar concatenations. This avoids literal residue searches but makes audits less honest. Replace with an explicit allowlist-based audit contract instead of hidden strings.
- `DatabaseProfileResolutionSource.LegacyDiscovery` remains in `DatabaseProfileModels.cs`. It is not clearly needed after legacy SQLite profile removal.
- `DatabaseProfileStorageMode.ManagedPerProfile` remains. Verify whether it is still meaningful for PostgreSQL-only runtime or a managed-SQLite remnant.
- `SwitchableAppDbContextFactory` still resolves current profile and creates DbContext options per context.
- `DatabaseRuntimeState` still wraps every current-profile context in a lease and blocks new contexts during hot switch.
- `DatabaseSwitchCoordinator` still performs hot database switching with a drain timeout.
- `DatabaseTransferService` still lists all non-target profiles as transfer sources. If `InMemory` is retained only for tests/explicit override, transfer source/target lists must filter to PostgreSQL runtime profiles.
- `ProcessRunAutomationDispatchService` uses static per-step `SemaphoreSlim` guards that wrap a long-running agent/workflow execution path. This may preserve step canonicality, but it is in-memory and process-local; for multi-worker PostgreSQL runtime it should be reduced to short claim/finalization windows and backed by durable DB claims.
