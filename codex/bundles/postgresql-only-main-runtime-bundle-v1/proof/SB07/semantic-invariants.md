# Semantic invariants - SB07

## Invariants to preserve

- Main application behavior unrelated to SQLite removal remains intact.
- CanDoItAll.IPFS remains untouched.
- No hidden SQLite runtime provider remains.
- PostgreSQL behavior is not weakened.
- Tests are not weakened to `InMemory` unless the test is explicitly a pure unit test.

## Subbundle-specific invariants

- Snapshot flows do not perform SQLite-backed runtime clone/export/restore work.
- Deferred snapshot operations return explicit failure/deferred results.
- Snapshot source kinds remain only for legacy/deferred catalog messaging.
