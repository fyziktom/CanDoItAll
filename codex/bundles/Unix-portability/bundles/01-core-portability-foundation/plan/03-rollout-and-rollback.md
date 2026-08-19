# Core rollout and rollback

## Rollout principles

- Develop and test against copies of existing Windows data.
- Keep compatibility readers before changing writers.
- Make migrations explicit, versioned, resumable, and dry-runnable.
- Preserve old key rings/payloads/catalogs until restart verification.
- Roll out headless core first; optional runtime/desktop capabilities stay disabled.
- Support claims advance per exact OS/profile/RID only after evidence.

## Recommended stages

1. Developer-only path/filesystem changes with no data rewrite.
2. Dual-read/new-write logical paths behind migration status.
3. Storage/control-plane backup and dry-run.
4. Storage/control-plane migration with old data retained.
5. Secure provider/key-ring bootstrap configured.
6. Secret migration on authorized source host.
7. Windows canary restart and rollback rehearsal.
8. Ubuntu headless canary.
9. macOS interactive/headless canary.
10. Active CI required and Core C4 handoff.

## Rollback invariants

Rollback must retain:

- the previous application binary;
- previous control-plane/catalog files and checksums;
- old database backup or transaction point;
- old logical/path records;
- old Data Protection key ring/protector;
- DPAPI/legacy secret payloads;
- vault generations and migration journal;
- service/config files and ownership/modes.

Do not clean source generations merely because a new write succeeded. Wait for pointer commit, restart verification, and the configured grace checkpoint.

## Rollback blockers

Stop rollback and invoke A91/A92 when:

- current and old generations are ambiguous;
- an old key ring/protector is missing;
- records were destructively rewritten without backup;
- a path points outside verified roots;
- a profile contains foreign syntax with no known source host;
- a storage catalog revision/content identity cannot be reconciled.
