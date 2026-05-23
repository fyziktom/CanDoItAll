# Shared implementation prompt

You are executing a PostgreSQL-only runtime cleanup in CanDoItAll.

Rules:

- Work on branch `development`.
- Read repository-local bundle execution skills first.
- Execute the assigned subbundle only.
- Preserve code comments in English.
- Do not modify CanDoItAll.IPFS.
- Do not keep SQLite as a hidden provider.
- Do not weaken PostgreSQL integration tests to `InMemory`.
- After each change group, run a targeted audit and record evidence.
- Update proof manifests.

Required evidence per subbundle:

```text
proof/SBxx/manifest.md
proof/SBxx/semantic-invariants.md
evidence/SBxx/build-or-test.log
evidence/SBxx/sqlite-audit.log
```

If a validation cannot be run, record the exact reason and what remains unproven.
