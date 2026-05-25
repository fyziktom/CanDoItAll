# Semantic invariants - SB03

## Invariants to preserve

- Main application behavior unrelated to SQLite removal remains intact.
- CanDoItAll.IPFS remains untouched.
- No hidden SQLite runtime provider remains.
- PostgreSQL behavior is not weakened.
- Tests are not weakened to `InMemory` unless the test is explicitly a pure unit test.

## Subbundle-specific invariants

- UI paths create PostgreSQL profiles only.
- Legacy SQLite profiles remain visible only as unsupported states for operator cleanup.
- Removed snapshot/SQLite controls do not leave active hidden actions behind.
