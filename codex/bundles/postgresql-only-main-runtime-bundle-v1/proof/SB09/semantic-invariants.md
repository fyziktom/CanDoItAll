# Semantic invariants - SB09

## Invariants to preserve

- Main application behavior unrelated to SQLite removal remains intact.
- CanDoItAll.IPFS remains untouched.
- No hidden SQLite runtime provider remains.
- PostgreSQL behavior is not weakened.
- Tests are not weakened to `InMemory` unless the test is explicitly a pure unit test.

## Subbundle-specific invariants

- Final validation evidence covers build, unit tests, targeted component tests, full integration tests, browser smoke proof, and audits.
- No proof artifact claims automatic SQLite data migration.
- Documentation states PostgreSQL-only runtime behavior and residual manual alignment requirements.
