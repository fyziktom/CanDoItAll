# Follow-up requirements

## FR-01: SQLite must be removed from main runtime models

Remove from `src/` runtime code:
- `DatabaseProviderKind.Sqlite`
- `DatabaseProfileSourceKind.ManagedSqlite`
- `DatabaseProfileSourceKind.ExternalSqliteFile`
- `DatabaseProfileSourceKind.ImportedSqlite`
- `SqliteDatabaseProfileConnection`
- `DatabaseProfileEditorModel.SqliteDatabasePath`
- SQLite branches in control-plane, startup resolver, DB factory, UI, tests.

## FR-02: Legacy SQLite catalog entries must not brick startup

Implement a raw JSON compatibility/quarantine layer that:
- detects legacy SQLite profile entries before deserializing into the new strongly typed model,
- writes a backup/quarantine artifact under the control-plane folder,
- removes or ignores legacy SQLite profiles,
- resets active profile if it pointed at a removed legacy profile,
- creates/selects PostgreSQL default if no valid PostgreSQL profile remains,
- logs operator-friendly instructions.

## FR-03: Main UI must be PostgreSQL-focused

Data Sources UI must:
- not reference `DatabaseProviderKind.Sqlite`,
- not render unsupported legacy SQLite forms,
- not expose snapshot actions,
- not expose persisted InMemory profile creation unless explicitly test-only/development-only.

## FR-04: Snapshot stubs must be removed or explicitly isolated

Because snapshots are deferred, runtime code should not keep active service/model bloat unless another current feature needs it. Remove:
- `IDatabaseSnapshotService` if unused,
- snapshot transport/request/result models if unused,
- snapshot deferred UI section,
- DI registration for `DatabaseSnapshotService`.

If any current compile dependency requires keeping them, move them to a clearly named `DeferredDatabaseSnapshotFeature` document-only placeholder and keep them out of runtime profile models.

## FR-05: Tests must enforce no SQLite residue

Add tests/scripts that fail when SQLite remains in main runtime code. Allowed residues:
- the new follow-up bundle text itself,
- historical docs if intentionally retained,
- external repository references only if explicit.

No SQLite residue should remain in:
- `src/**/*.cs`
- `src/**/*.razor`
- `tests/**/*.cs`
- `*.csproj`
- `CanDoItAll.slnx`

## FR-06: PostgreSQL baseline migration must be proven

Prove:
- one baseline migration only,
- fresh DB can be created,
- `dotnet ef migrations add __DriftCheck` produces no meaningful changes or is removed after verification,
- schema initializers do not hide migration omissions.

## FR-07: Process/workflow runtime must be PostgreSQL-tuned

Audit and tune:
- process run dispatch,
- workflow runtime persistence,
- automation envelopes/outbox,
- plugin command outbox,
- scheduler/planner durable jobs,
- background job tracking if it is used for durable execution.

Use PostgreSQL-safe transaction/claim patterns and add negative concurrency tests.

## FR-08: Unrelated artifacts must be cleaned

Review unrelated changes and artifacts. Either:
- justify them in the SQLite-removal execution report, or
- move/revert them from `db-remove-sqlite`.

## NFR-01: Build/test/browser validation

Required final validation:
- `dotnet restore .\CanDoItAll.slnx`
- `dotnet build .\CanDoItAll.slnx -m:1 -v:minimal`
- unit tests
- component tests for Data Sources
- integration tests excluding explicit live/browser categories as appropriate
- fresh PostgreSQL migration proof
- Playwright/browser proof for Data Sources UI
- residue audit.
