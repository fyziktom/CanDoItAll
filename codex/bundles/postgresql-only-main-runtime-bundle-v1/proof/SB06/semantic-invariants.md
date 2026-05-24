# Semantic invariants - SB06

## Invariants to preserve

- Main application behavior unrelated to SQLite removal remains intact.
- CanDoItAll.IPFS remains untouched.
- No hidden SQLite runtime provider remains.
- PostgreSQL behavior is not weakened.
- Tests are not weakened to `InMemory` unless the test is explicitly a pure unit test.

## Subbundle-specific invariants

- Process/workflow/outbox code relies on PostgreSQL-compatible SQL and EF translation.
- Activity timeline queries order before projection so PostgreSQL can translate them.
- Concurrent process assignment resolution preserves one row per run/role/step scope.
