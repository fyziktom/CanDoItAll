# Semantic invariants - SB02

## Invariants to preserve

- Main application behavior unrelated to SQLite removal remains intact.
- CanDoItAll.IPFS remains untouched.
- No hidden SQLite runtime provider remains.
- PostgreSQL behavior is not weakened.
- Tests are not weakened to `InMemory` unless the test is explicitly a pure unit test.

## Subbundle-specific invariants

- Empty catalogs auto-provision PostgreSQL profiles.
- Persisted legacy SQLite profiles are not silently activated.
- Runtime switching and workspace services resolve against the selected PostgreSQL profile.
