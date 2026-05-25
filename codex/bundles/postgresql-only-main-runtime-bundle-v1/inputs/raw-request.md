# Raw user request summary

The user wants to remove SQLite from the main CanDoItAll application on branch `development`.

Motivation:

- Running process/workflow logic with SQLite is slow.
- SQLite increases codebase size.
- Double migrations are expensive.
- SQLite-specific compatibility limits architecture and asynchronicity.
- SQLite was useful at the beginning for project structure and might be useful for future snapshots, but it is no longer worth the maintenance burden.
- Snapshot support can be reimplemented later if needed.

Required output:

- Detailed execution-grade bundle.
- Use repository-local bundle skills.
- Split work into phases/subbundles.
- First remove SQLite and related migrations/driver.
- Then remove SQLite UI.
- Then modify driver/runtime limitations that existed because of SQLite.
- Only after general limitations are removed, perform process/workflow-specific changes.
- Remove SQLite tests.
- Consolidate PostgreSQL migrations ideally into one baseline.
