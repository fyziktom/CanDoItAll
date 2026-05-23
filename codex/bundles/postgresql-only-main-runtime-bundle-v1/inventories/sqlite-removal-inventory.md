# SQLite removal inventory

Codex must update this inventory while executing the bundle.

## Runtime/provider

| Surface | Expected action |
|---|---|
| `SwitchableAppDbContextFactory.cs` | Remove SQLite branch and SQLite migrations assembly constant |
| `AppDbContextOptionsConfigurator` | PostgreSQL-only persistent runtime configuration |
| `AppDbContextFactory` | Default to PostgreSQL, not SQLite |
| SQLite connection normalization/interceptors | Remove from main runtime |
| `Microsoft.EntityFrameworkCore.Sqlite` | Remove from main runtime projects |

## Driver/control plane

| Surface | Expected action |
|---|---|
| `DatabaseProviderKind.Sqlite` | Remove or convert to unsupported legacy marker outside runtime |
| `SqliteDatabaseDriver` | Remove |
| `DatabaseDriverRegistry` | Register PostgreSQL only |
| SQLite fingerprint/path helpers | Remove |
| SQLite profile source kinds | Remove or mark unsupported legacy only |
| Legacy SQLite profile discovery | Remove |

## UI

| Surface | Expected action |
|---|---|
| Managed SQLite action | Remove |
| Open SQLite action | Remove |
| SQLite file path fields | Remove |
| SQLite empty state copy | Replace with PostgreSQL onboarding |
| Snapshot materialized path copy | Remove/defer |

## Web/dev endpoints

| Surface | Expected action |
|---|---|
| `/_dev/database/profiles/managed-sqlite` | Remove |
| Any SQLite dev bootstrap | Remove |

## Tests

| Surface | Expected action |
|---|---|
| `TestDatabaseProviderKind.Sqlite` | Remove |
| SQLite test profile factory | Remove |
| SQLite package references | Remove |
| SQLite-only tests | Delete or convert |
| Persistence integration tests | PostgreSQL-backed |

## Snapshot flows

| Surface | Expected action |
|---|---|
| SQLite materialized snapshot profiles | Remove/defer |
| `SnapshotCache` | Remove/defer |
| `IpfsSnapshot` | Remove/defer |
| SQLite PRAGMA restore logic | Remove |

## Migrations

| Surface | Expected action |
|---|---|
| `src/CanDoItAll.Migrations.Sqlite/` | Delete |
| `src/CanDoItAll.Migrations.PostgreSql/Migrations/*` | Consolidate after model is stable |
| `CanDoItAll.slnx` | Remove SQLite migration project |
