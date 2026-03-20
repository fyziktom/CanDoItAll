# Master checklist

## Architecture
- [x] Add a shared repository domain instead of extending each version table separately.
- [x] Keep existing entity tables as read models / compatibility bridges.
- [x] Use content-addressed blobs and snapshots.
- [x] Preserve exact commit pinning for purchases/shares/publication.

## Database and storage
- [x] Add repository schema migration.
- [x] Add repository storage config roots.
- [x] Add canonical hashing helpers.
- [x] Add blob/snapshot/commit persistence helpers.
- [x] Add repository verification tooling.

## Backfill and compatibility
- [x] Backfill score histories.
- [x] Backfill learning package histories.
- [x] Backfill playlist histories.
- [x] Create event repositories.
- [x] Add legacy-version-to-commit mapping.

## Branches and merge
- [x] Add branch refs.
- [x] Protect default branch.
- [x] Add fast-forward and merge-commit support.
- [x] Add compare endpoint.
- [x] Add merge-preview endpoint.
- [x] Add score merge contracts and structured hunk payloads.

## Forks and merge requests
- [x] Add fork policy enforcement.
- [x] Add fork creation.
- [x] Add merge request create/list/detail.
- [x] Add MR merge and close actions.
- [x] Add legal/listing restrictions for non-owner forks.

## PHP UI
- [x] Add repository graph canvas module.
- [x] Show graph on score detail/manage screens.
- [x] Show graph on playlist pages.
- [x] Show graph on event pages.
- [x] Show graph on learning package builder pages.

## WASM/offline
- [x] Add origin status batch endpoint.
- [x] Add pull batch endpoint.
- [x] Make DTOs suitable for IndexedDB storage.
- [x] Keep localStorage use minimal.

## Tests and seeds
- [x] Add unit tests for hashing/refs/merge logic.
- [x] Add API smoke test for repository flows.
- [x] Extend seed data with branches/forks/MRs.
- [x] Document migration/backfill limitations honestly.

## Final audit
- [x] Verify identical content reuses blobs.
- [x] Verify non-default branch does not overwrite read model.
- [x] Verify purchases/shares pin commit hashes.
- [x] Verify graph renders usable history.
