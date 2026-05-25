# Assumptions and risks

## Assumptions

- The target branch is `development`.
- The user has only one real PostgreSQL database with real data.
- Manual real database alignment is acceptable.
- SQLite snapshot support can be removed/deferred and reimplemented later.
- CanDoItAll.IPFS is out of scope and can keep its isolated SQLite index.
- PostgreSQL local development is acceptable.

## Key risks

### Risk: stale local SQLite profile catalog breaks startup

Mitigation:

- Do not silently fallback.
- Detect unsupported SQLite catalog entries.
- Show clear error or migration guidance.
- Prefer a one-time catalog cleanup command or documented manual cleanup.

### Risk: tests are weakened

Mitigation:

- Do not replace persistence tests with `InMemory`.
- Use PostgreSQL-backed test fixture for integration behavior.
- Keep `InMemory` only for narrow pure unit tests.

### Risk: migration consolidation destroys migration history needed by real DB

Mitigation:

- Consolidate only after model stabilizes.
- Generate manual real-db alignment guide.
- Do not promise automatic live DB migration unless a tested script exists.

### Risk: process/workflow tuning is attempted too early

Mitigation:

- Finish SQLite removal, UI cleanup, and test support conversion before tuning.
- Introduce general PostgreSQL runtime primitives before touching process-specific code.

### Risk: hidden SQLite dependencies remain

Mitigation:

- Repeated ripgrep audits.
- Package reference audits.
- Solution/project audits.
- UI text audits.
- Test audits.
