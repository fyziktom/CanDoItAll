# Out of scope

The following are explicitly out of scope for this bundle:

- Modifying `CanDoItAll.IPFS`.
- Removing SQLite from isolated non-main-runtime utility stores outside CanDoItAll main app.
- Implementing a new snapshot/export/import system.
- Implementing automatic migration of the user's one real PostgreSQL database unless separately requested and tested.
- Replacing PostgreSQL integration behavior with `InMemory` tests.
- Large unrelated refactors of UI, workflows, plugins, or process models.
- Rewriting the database abstraction layer beyond what is necessary for PostgreSQL-only runtime.
