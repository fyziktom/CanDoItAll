# SB01 - Remove SQLite Runtime Provider, Driver, Dependencies, and Migration Project

## Objective

Remove SQLite from the main runtime persistence path.

## Inputs

Known files:

```text
src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs
src/CanDoItAll.Infrastructure/ControlPlane/DatabaseDrivers.cs
src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs
src/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj
src/CanDoItAll.Migrations.Sqlite/
CanDoItAll.slnx
src/CanDoItAll.Web/Infrastructure/RuntimeDatabaseSwitching.cs
src/CanDoItAll.Web/Infrastructure/DatabaseMigrationBootstrap.cs
```

## Required changes

- Remove SQLite migrations assembly constant.
- Remove `UseSqlite(...)` and SQLite EF provider configuration.
- Remove SQLite connection-string normalization from main runtime.
- Remove SQLite write coordination/interceptor from main runtime.
- Remove `SqliteDatabaseDriver`.
- Remove SQLite provider registration from DI.
- Remove SQLite migration project from solution.
- Remove SQLite package references from main runtime projects.
- Make PostgreSQL the default persistent provider.
- Remove legacy SQLite migration bootstrap paths.
- Keep `InMemory` only if it remains explicitly needed for narrow tests.

## Do not

- Do not modify CanDoItAll.IPFS.
- Do not keep SQLite behind a feature flag.
- Do not implement snapshot replacement in this phase.

## Validation

```powershell
dotnet build .\CanDoItAll.slnx
rg -n -i "usesqlite|migrations\.sqlite|sqlitewritecoordination|legacysqlitemigrationbootstrap|CanDoItAll\.Migrations\.Sqlite" src tests
```

## Required proof

```text
proof/SB01/manifest.md
proof/SB01/semantic-invariants.md
evidence/SB01/sqlite-runtime-audit.log
evidence/SB01/build.log
```
