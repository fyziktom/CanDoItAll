# Rollout and rollback

## Source changes

Each subbundle should remain independently reviewable. Do not combine P0 persistence migration, FileTools source-mode changes, and process-tree ownership into one unreviewable patch.

## Persisted plan migration

- take a backup or transactionally retain the original serialized payload/hash;
- verify the legacy hash before mutation;
- write the current payload/hash only after capability derivation succeeds;
- on failure, retain the original and mark typed remediation;
- provide dry-run/reporting and idempotent restart behavior.

## Process ownership

Introduce the ownership boundary behind the existing process-host interface. Preserve a feature switch only if needed for controlled rollback; do not keep two independent production process-launch implementations.

## FileTools

Package mode remains safe fallback. If explicit direct-source validation fails, disable the desktop capability rather than falling back to an unverified source claim.

## Docker

The local stack must remain opt-in and loopback-bound. Never commit `.secrets/db-password`. Teardown must remove disposable containers/networks while preserving deliberately named data volumes only when requested.

## Final rollback boundary

Do not merge until the exact candidate has:

- reversible/typed persisted-plan handling;
- no orphan process-tree regression;
- clean package-mode build without sibling repositories;
- reproducible FileTools source-mode decision;
- successful M08 evidence;
- successful or explicitly reviewed M09 macOS evidence.
