# SB05 - Remove General SQLite-Era Runtime Limitations

## Objective

Remove general runtime limitations that existed only because SQLite was supported.

This must happen before process/workflow-specific tuning.

## Audit areas

```text
src/CanDoItAll.Infrastructure/Persistence/**
src/CanDoItAll.Infrastructure/DependencyInjection/**
src/CanDoItAll.Infrastructure/BackgroundJobs/**
src/CanDoItAll.Web/Infrastructure/RuntimeDatabaseSwitching.cs
src/CanDoItAll.Web/Infrastructure/DatabaseMigrationBootstrap.cs
```

## Look for

- Single-writer assumptions.
- Conservative global semaphores.
- Drain/lease behavior designed around SQLite switching.
- Provider-neutral APIs that prevent PostgreSQL-native concurrency.
- Artificial low worker concurrency defaults.
- Retry/transaction behavior weakened for SQLite compatibility.
- Database switching behavior that still assumes file-backed local DBs.

## Required changes

- Simplify runtime database switching now that persistent runtime is PostgreSQL-only.
- Remove SQLite-related drain constraints.
- Introduce or clean up PostgreSQL-oriented runtime primitives where needed:
  - transaction-safe claiming,
  - lease boundaries,
  - retry policies,
  - connection health checks,
  - migration bootstrap clarity.
- Keep changes generic; do not deeply tune process/workflow modules yet.

## Validation

```powershell
rg -n -i "single writer|single-writer|sqlite|database switching|drain|lease|semaphore|worker concurrency" src/CanDoItAll.Infrastructure src/CanDoItAll.Web
dotnet build .\CanDoItAll.slnx
```

## Required proof

```text
proof/SB05/manifest.md
proof/SB05/semantic-invariants.md
evidence/SB05/runtime-limitations-audit.md
evidence/SB05/build.log
```
