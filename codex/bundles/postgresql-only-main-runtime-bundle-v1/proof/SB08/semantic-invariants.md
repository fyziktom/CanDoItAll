# Semantic invariants - SB08

## Invariants to preserve

- Main application behavior unrelated to SQLite removal remains intact.
- CanDoItAll.IPFS remains untouched.
- No hidden SQLite runtime provider remains.
- PostgreSQL behavior is not weakened.
- Tests are not weakened to `InMemory` unless the test is explicitly a pure unit test.

## Subbundle-specific invariants

- PostgreSQL migrations are represented by one baseline migration and matching model snapshot.
- Fresh PostgreSQL databases migrate from empty state through the baseline.
- Real existing databases require manual backup and schema/history alignment.
