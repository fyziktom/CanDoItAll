# Branch review

## What Codex appears to have completed

The second Codex pass closed the most obvious typed SQLite residue:

- Removed retired SQLite provider/source enum values from `DatabaseProfileModels.cs`.
- Removed the retired SQLite connection profile model and editor field.
- Removed the snapshot runtime service and DI registration.
- Removed SQLite-specific factory/provider branches from `SwitchableAppDbContextFactory`.
- Kept `InMemory` as a non-persistent test/override path.
- Added raw JSON quarantine for legacy database profile catalogs before typed deserialization.
- Removed SQLite/snapshot controls from the Data Sources UI.
- Consolidated PostgreSQL migrations to a single baseline.
- Updated tests and reports to claim PostgreSQL-only validation.

## What remains incomplete or risky

### Branch state

The branch is diverged from `development`. It must be rebased or merged before final validation; otherwise the current proof can be stale.

### Audit honesty

`LegacyDatabaseProfileCatalogQuarantine` constructs retired provider/source strings using concatenation (`"Sql" + "ite"`). This should be replaced by an explicit allowlist in the residue audit. It is better to have a deliberate, documented quarantine exception than to hide strings from grep.

### Canonical runtime DB model

The runtime still behaves like it can hot-switch profiles at any time. Normal `AppDbContext` creation still resolves the active profile and participates in the runtime switch lease. That makes every DB operation pay for a rarely-used admin feature and keeps the architecture shaped by legacy profile-switching concerns.

### Bottleneck remnants

The biggest remaining bottlenecks are not SQLite references, but SQLite-era safety patterns:

- global context lease/drain for every context,
- per-context options creation,
- hot switch coordination inside normal runtime path,
- process dispatch in-memory step semaphores covering long-running agent work,
- per-row/per-delivery claim patterns where PostgreSQL can claim batches atomically.

### Canonicality risk

Removing guards blindly would be dangerous. The right direction is not "remove all locks"; it is:

- keep one canonical DB per process generation,
- replace process-local locks with durable PostgreSQL claims,
- restrict DB profile switching to maintenance/restart flow,
- prove duplicate-claim prevention with concurrent integration tests.
