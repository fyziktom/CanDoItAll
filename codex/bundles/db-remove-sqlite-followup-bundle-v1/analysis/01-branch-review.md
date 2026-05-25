# Branch review: what Codex completed vs. missed

## Completed well

1. **SQLite migration project removal**
   - The solution no longer contains `CanDoItAll.Migrations.Sqlite`.
   - This removes the largest duplicated-migration burden.

2. **SQLite EF package removal**
   - `Microsoft.EntityFrameworkCore.Sqlite` is gone from Infrastructure.
   - Runtime `UseSqlite(...)` setup is no longer present.

3. **SQLite write coordination removal**
   - `SqliteWriteCoordination.cs` was deleted.
   - SQLite-specific connection interceptors and WAL/busy-timeout setup are no longer in the main EF path.

4. **PostgreSQL as default**
   - Main options and design-time DB factory default to PostgreSQL.

5. **Snapshot materialization no longer creates SQLite runtime profiles**
   - Snapshot service is now a deferred failure instead of a SQLite-backed clone/materialize path.

6. **Test support moved away from SQLite**
   - Test provider enum is PostgreSQL/InMemory only.
   - Integration project no longer has Microsoft.Data.Sqlite package reference.
   - PostgreSQL test lease support was added.

7. **Data Sources UI no longer offers new/open SQLite actions**
   - The primary action is now PostgreSQL.
   - Empty state says "Start with a PostgreSQL profile".

## Partial / incomplete

1. **SQLite was not removed from the domain model**
   - `DatabaseProviderKind.Sqlite` remains.
   - SQLite source kinds remain.
   - SQLite profile connection model remains.
   - Editor still has `SqliteDatabasePath`.

2. **SQLite is still represented as a supported legacy state**
   - Control-plane contains explicit SQLite validation/rejection/descriptor branches.
   - Startup resolver contains SQLite failure branches.
   - Switchable factory contains a SQLite case that throws.

3. **Legacy catalog handling is fragile**
   - If a persisted active profile is SQLite, startup/resolution throws.
   - `ListAsync()` resolves the current profile before listing, so legacy SQLite can prevent operators from even opening the UI to fix/remove it.
   - This should become a raw JSON quarantine/migration step, not an enum-model branch.

4. **UI still has SQLite branch**
   - It still renders an "Unsupported legacy profile" section for SQLite.
   - This keeps SQLite in Blazor code and test surface.

5. **Snapshot service still contributes runtime/API surface**
   - It is deferred, but the models/service remain part of Infrastructure.
   - The user said snapshot support can be reimplemented later, so remove the stubs unless they are explicitly required by current UI/API contracts.

6. **InMemory is still a persisted profile kind**
   - InMemory is fine for tests, but it should not be a normal persisted runtime profile in the main Data Sources model unless deliberately kept.
   - Consider splitting test-only factory support from persisted runtime profile provider model.

7. **PostgreSQL process/workflow tuning is not proven**
   - The follow-up must audit actual durable execution services and add negative concurrency tests.
   - Build/test success is insufficient evidence for high-concurrency workflow correctness.

8. **Unrelated/stale branch artifacts**
   - The root execution report appears stale/unrelated.
   - Added `.codex/bundles/project-structure-workflow-runs/proof/...` artifacts look unrelated to SQLite removal.
