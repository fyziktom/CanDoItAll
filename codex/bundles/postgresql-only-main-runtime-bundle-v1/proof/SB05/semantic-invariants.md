# Semantic invariants - SB05

## Invariants to preserve

- Main application behavior unrelated to SQLite removal remains intact.
- CanDoItAll.IPFS remains untouched.
- No hidden SQLite runtime provider remains.
- PostgreSQL behavior is not weakened.
- Tests are not weakened to `InMemory` unless the test is explicitly a pure unit test.

## Subbundle-specific invariants

- Runtime code no longer branches around SQLite write, schema, or query limitations.
- Stale tooling and prompt-library generator text do not reintroduce SQLite guidance.
- Remaining SQLite strings are legacy unsupported-state handling or rejection tests.
