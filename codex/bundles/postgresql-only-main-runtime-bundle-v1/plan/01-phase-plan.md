# Phase plan

## Phase 0 - Preflight

- Confirm branch `development`.
- Run initial audit.
- Capture current build/test state.
- Read local bundle execution skills.
- Create evidence folders.

Suggested commands:

```powershell
git status
git branch --show-current
rg -n -i "sqlite|usesqlite|migrations\.sqlite|managedsqlite|externalsqlite|importedsqlite|sqlitewritecoordination|legacysqlitemigrationbootstrap|snapshotcache|ipfssnapshot" src tests docs
dotnet build .\CanDoItAll.slnx
```

## Phase 1 - SQLite runtime removal

Subbundles:

- SB01
- SB02

Goal:

- No runtime provider branch can create SQLite `AppDbContext`.

## Phase 2 - UI and test cleanup

Subbundles:

- SB03
- SB04

Goal:

- Users cannot select/create SQLite profiles.
- Tests no longer rely on SQLite.

## Phase 3 - Runtime limitations cleanup

Subbundle:

- SB05

Goal:

- Remove generic restrictions that existed only for SQLite.
- Introduce PostgreSQL-oriented primitives if needed.

## Phase 4 - Process/workflow tuning

Subbundle:

- SB06

Goal:

- Use PostgreSQL-only assumptions to improve workflow/process/automation/outbox concurrency and correctness.

## Phase 5 - Snapshot removal/defer

Subbundle:

- SB07

Goal:

- Current SQLite-backed snapshot profile flows are removed or explicitly deferred.

## Phase 6 - PostgreSQL migration consolidation

Subbundle:

- SB08

Goal:

- One clean PostgreSQL baseline migration after the model is stable.

## Phase 7 - Final validation

Subbundle:

- SB09

Goal:

- Build/test/browser/migration/audit evidence.
