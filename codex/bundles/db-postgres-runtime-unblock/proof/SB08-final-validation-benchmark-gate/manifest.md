# SB08 Proof Manifest

## Subbundle

SB08-final-validation-benchmark-gate — Completed with documented validation blocker.

Owned requirements: R1, R10, R12.

Semantic invariant contract: `bundle://proof/SB08-final-validation-benchmark-gate/semantic-invariants.md`.

## Changed Files

| File | SHA-256 before | SHA-256 after | Reason |
|---|---|---|---|
| `bundle://proof/SB08-final-validation-benchmark-gate/changed-file-hashes.tsv` | New | Current file | Portable before/after hash inventory for changed source/test/bundle files. |
| `bundle://scripts/validate_bundle.py` | New | Current file | Adds bundle readiness/completion validator. |
| `bundle://reviews/01-execution-report.md` | New | Current file | Records final implementation, proof, blockers, and raw-note closure. |
| `repo://src/**` and `repo://tests/**` changed files | See hash inventory | See hash inventory | Runtime, profile, outbox, dispatch, UI, API, and test changes. |

## Commands

| Command | Transcript path | Result |
|---|---|---|
| `dotnet restore .\CanDoItAll.slnx` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-restore.txt` | Passed. |
| `dotnet build .\CanDoItAll.slnx -m:1 -v:minimal` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-build-final.txt` | Passed with existing EF Core relational version warnings. |
| Unit tests | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-unit-full.txt` | Passed 788 tests. |
| Data Sources component tests | `bundle://proof/SB04-maintenance-restart-db-activation/transcripts/dotnet-test-components-database-sources.txt` | Passed 10 tests. |
| Focused PostgreSQL integration sweep | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt` | Passed 452 tests. |
| Playwright Data Sources test | `bundle://proof/SB04-maintenance-restart-db-activation/transcripts/dotnet-test-playwright-database-switch.txt` | Passed 1 test. |
| EF pending model changes | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-ef-has-pending-model-changes.txt` | Passed; no pending model changes. |
| Residue/bottleneck audit | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/audit-residue-and-bottlenecks.txt` | Passed. |
| Source assertions | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt` | Passed. |
| Anti-stub audit | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/anti-stub-audit.txt` | Passed. |
| Bundle prepared validator | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/validate-bundle-prepared.txt` | Passed. |
| Bundle completed validator | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/validate-bundle-completed.txt` | Passed. |
| Broad non-quarantined integration sweep | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-nonquarantined.txt` | Blocked by local PostgreSQL auth/time-out; not counted as pass. |

## Semantic Positive Proof

The final validation set proves the changed runtime, profile activation, PostgreSQL claim, process dispatch, migration, and UI paths with build, tests, browser screenshots, EF drift, source assertions, residue audit, and anti-stub audit.

## Adversarial Negative Proof

The final report records the two non-passing environment checks instead of treating them as hidden success: remote fetch SSH auth and broad integration PostgreSQL credentials. Focused integration proof covers the changed high-risk surfaces.

## Canonicality Proof

SB03-SB07 manifests and invariant contracts prove one canonical runtime profile per process generation, restart-first activation, PostgreSQL batch/durable claims, and transfer/admin separation.

## Anti-Stub Audit

`bundle://proof/SB08-final-validation-benchmark-gate/transcripts/anti-stub-audit.txt` found no stub markers in changed production files. `bundle://proof/SB08-final-validation-benchmark-gate/fake-proof-red-team.md` rechecks fake-proof resistance across critical subbundles.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Canonical runtime profile | `bundle://proof/SB03-canonical-runtime-db-pooled-factory/manifest.md` | `bundle://proof/SB03-canonical-runtime-db-pooled-factory/semantic-invariants.md` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt` |
| Restart-required activation result | `bundle://proof/SB04-maintenance-restart-db-activation/manifest.md` | `bundle://proof/SB04-maintenance-restart-db-activation/semantic-invariants.md` | `bundle://proof/SB04-maintenance-restart-db-activation/transcripts/dotnet-test-playwright-database-switch.txt` | `bundle://proof/SB04-maintenance-restart-db-activation/transcripts/dotnet-test-components-database-sources.txt` |
| PostgreSQL work leases | `bundle://proof/SB05-postgresql-batch-claim-outbox/manifest.md` and `bundle://proof/SB06-process-dispatch-durable-leases/manifest.md` | Their invariant contracts | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt` |

## Browser Validation Analytics

See SB04 manifest and `bundle://reviews/01-execution-report.md`.

## Remaining Risks

Before merge, rerun remote branch ancestry and broad non-quarantined integration in an environment with repository SSH access and the expected PostgreSQL test credentials.
