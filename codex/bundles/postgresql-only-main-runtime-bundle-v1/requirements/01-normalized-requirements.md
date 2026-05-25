# Normalized requirements

## Functional requirements

REQ-001: The main CanDoItAll runtime must no longer support SQLite as a persistent database provider.

REQ-002: PostgreSQL must become the only persistent runtime database provider.

REQ-003: The SQLite migration project must be removed from the solution/build.

REQ-004: SQLite-related package references must be removed from main runtime projects.

REQ-005: SQLite UI actions and settings must be removed from Workspace/Data Sources UI.

REQ-006: Dev endpoints that create managed SQLite profiles must be removed.

REQ-007: SQLite-based tests and test support must be removed or converted to PostgreSQL-backed tests.

REQ-008: Runtime limitations that existed only because SQLite was supported must be removed before process/workflow-specific tuning.

REQ-009: Process, workflow, automation, outbox, and plugin execution logic must be reviewed for PostgreSQL-native concurrency improvements.

REQ-010: SQLite-backed snapshot profile/materialization flows must be removed or explicitly deferred.

REQ-011: PostgreSQL migrations should be consolidated into one baseline after the model is stable.

REQ-012: Provide manual real database alignment guidance for the user's one real database.

## Non-functional requirements

NFR-001: Preserve maintainability by reducing provider branching.

NFR-002: Preserve or improve build speed by removing duplicate SQLite migrations.

NFR-003: Preserve correctness under concurrent workflow/process execution.

NFR-004: Avoid hidden fallbacks or silent compatibility behavior.

NFR-005: Preserve clear error messages for unsupported legacy SQLite profile entries.

NFR-006: Do not weaken test coverage by replacing PostgreSQL integration tests with `InMemory`.

NFR-007: Keep source code comments in English.
