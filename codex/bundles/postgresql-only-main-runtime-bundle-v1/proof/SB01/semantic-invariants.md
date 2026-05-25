# Semantic invariants - SB01

## Invariants to preserve

- Main application behavior unrelated to SQLite removal remains intact.
- CanDoItAll.IPFS remains untouched.
- No hidden SQLite runtime provider remains.
- PostgreSQL behavior is not weakened.
- Tests are not weakened to `InMemory` unless the test is explicitly a pure unit test.

## Subbundle-specific invariants

- No SQLite runtime provider can be resolved for the main app; explicit requests fail with an unsupported-provider error.
- No SQLite package, migration project, `UseSqlite`, `SqliteConnection`, or write-coordination reference remains in source/test/tool project surfaces.
- PostgreSQL remains the only persistent runtime provider; in-memory remains limited to explicit test/runtime override scenarios.
