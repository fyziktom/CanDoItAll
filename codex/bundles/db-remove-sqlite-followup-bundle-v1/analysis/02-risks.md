# Risks and architectural concerns

## Risk 1: Startup bricking on legacy SQLite catalog

If the control-plane catalog contains an old active SQLite profile, the current startup resolver throws. This can prevent the app from starting even though the goal is to move to PostgreSQL-only.

Required fix:
- do not model legacy SQLite entries as runtime records,
- pre-scan `catalog.json` as raw JSON,
- quarantine legacy SQLite entries/documents,
- reset active profile to a PostgreSQL profile or create a default PostgreSQL profile,
- log a clear warning with the quarantine path.

## Risk 2: Half-removed SQLite makes future Codex work ambiguous

Leaving `DatabaseProviderKind.Sqlite` and `DatabaseProfileSourceKind.ManagedSqlite` in the model invites future agents to generate new SQLite support by accident.

Required fix:
- delete SQLite enum values,
- delete SQLite connection models,
- delete SQLite UI branches,
- make `rg -n -i "sqlite"` fail in `src/` and `tests/` except explicitly allowed docs/bundle files.

## Risk 3: UI says snapshots exist but they do not

A "Snapshots deferred" section is informative, but it also keeps dead runtime surface in the app. Since the user explicitly said snapshots can be reimplemented later, the cleaner architecture is to remove the active runtime service and leave only documentation/future-work notes.

## Risk 4: PostgreSQL baseline may drift from model

The branch consolidated migrations, but the final gate must prove:
- clean PostgreSQL DB can be created from zero,
- no pending EF model diff exists,
- generated SQL script is valid,
- schema initializers do not compensate for missing migration objects.

## Risk 5: Workflow/process runtime may still be designed around SQLite constraints

Removing SQLite frees the runtime to use PostgreSQL primitives. The follow-up must look for and fix:
- low/serialized worker concurrency that existed to avoid SQLite locks,
- provider-neutral outbox claim logic,
- non-atomic claim/update loops,
- in-memory queues where durable PostgreSQL execution is required,
- missing negative tests for double-claim/double-dispatch.
