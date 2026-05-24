# SB08 proof manifest

## Status

Completed with documented validation caveats.

## Owned requirements

Final restore/build/test/audit/EF validation, throughput proof, canonical runtime DB proof, stale-claim proof, and merge recommendation.

## Changed files

All changed source, tests, bundle scripts, and proof artifacts are listed with before/after SHA-256 data in `bundle://proof/SB08/transcripts/changed-file-hashes.txt`.

## Command transcripts

- `bundle://proof/SB08/transcripts/dotnet-restore.txt`
- `bundle://proof/SB08/transcripts/full-solution-build-final-clean.txt`
- `bundle://proof/SB08/transcripts/full-unit-tests.txt`
- `bundle://proof/SB08/transcripts/focused-integration-tests.txt`
- `bundle://proof/SB08/transcripts/focused-component-tests.txt`
- `bundle://proof/SB08/transcripts/main-layout-component-tests-rerun.txt`
- `bundle://proof/SB02/transcripts/playwright-runtime-pending-switch-rerun.txt`
- `bundle://proof/SB08/transcripts/ef-pending-model-changes.txt`
- `bundle://proof/SB03/transcripts/residue-and-switch-audit-final.txt`
- `bundle://proof/SB08/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB08/transcripts/semantic-invariant-index.txt`

## Source assertions

- Runtime DB canonicality is represented separately from pending restart state.
- Hot-switch/drain state is removed from normal runtime.
- Profile-specific context creation is named as maintenance-only.
- Claimed PostgreSQL batches process with bounded parallelism and partitioning.
- Process dispatch mutation requires a current durable claim.
- Candidate hydration happens after durable claim where semantically safe.

## Semantic positive proof

Restore, final full build, full unit tests, focused integration tests, focused component tests, MainLayout component tests, managed-files runtime proof, and Playwright runtime/pending proof passed.

## Adversarial negative proof

Residue audit, anti-stub audit, claim source audit, and the failed-then-passing Playwright transcript prove the main shallow implementations were rejected.

## Residual risks

- Full integration suite timed out at 30 minutes; broad run also hit environment-specific PostgreSQL `28P01` authentication failures in database transfer tests. Focused integration coverage for this bundle passed.
- Full component suite timed out at 30 minutes after earlier stale label failures; focused touched component suites passed after fixes.
- Existing EF Core relational assembly conflict warnings (`MSB3277`) remain.
- No current-bundle screenshot artifact was generated; browser proof is transcript/assertion based.
- No numeric wall-clock throughput benchmark was captured; deterministic concurrency proof is documented in `bundle://proof/SB08/benchmark-report.md`.
