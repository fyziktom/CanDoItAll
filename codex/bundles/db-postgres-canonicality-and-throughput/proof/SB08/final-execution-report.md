# Final execution report

## Summary

The bundle is implemented. The branch now separates runtime canonical database state from pending restart activation, removes dead hot-switch/drain semantics, names profile-specific contexts as maintenance-only, parallelizes claimed PostgreSQL work with bounded partitioned processing, hardens process dispatch claim ownership, and moves process candidate hydration after durable claim where safe.

## Fulfillment matrix

| Subbundle | Result | Primary proof |
|---|---|---|
| SB01 scope and evidence cleanup | Solved with SSH fetch caveat | `bundle://proof/SB01/manifest.md` |
| SB02 runtime vs pending activation | Solved | `bundle://proof/SB02/manifest.md` |
| SB03 remove hot-switch/drain | Solved | `bundle://proof/SB03/manifest.md` |
| SB04 profile context boundaries | Solved | `bundle://proof/SB04/manifest.md` |
| SB05 bounded claimed work | Solved with deterministic proof, no numeric benchmark | `bundle://proof/SB05/manifest.md` |
| SB06 claim-token canonicality | Solved with source and focused integration proof | `bundle://proof/SB06/manifest.md` |
| SB07 claim-first loading | Solved with elapsed-time telemetry, no query-count capture | `bundle://proof/SB07/manifest.md` |
| SB08 final validation | Solved with broad-suite caveats | `bundle://proof/SB08/manifest.md` |

## Source files changed

The full changed-file list and before/after SHA-256 data is in `bundle://proof/SB08/transcripts/changed-file-hashes.txt`. Main touched areas:

- Runtime/control plane: database profile models, activation result contracts, runtime state, profile-specific context factory, bootstrap/switch coordinator.
- Workspace/Web UI/API: Data Sources panel, MainLayout database dialog, cognitive memory API DTOs/endpoints, dev database endpoints.
- Throughput: automation dispatch, connector outbox, process outbox, runtime options.
- Process dispatch: dispatch claim renewal, claim verification, artifact projection, completion recovery, claim-first candidate loading.
- Tests: runtime switch unit/integration, managed files, component, Playwright, process/automation/connector focused integration coverage.

## Build and test proof

- Restore passed: `bundle://proof/SB08/transcripts/dotnet-restore.txt`.
- Full solution build passed with 0 errors: `bundle://proof/SB08/transcripts/full-solution-build-final-clean.txt`.
- Full unit tests passed, 788 total: `bundle://proof/SB08/transcripts/full-unit-tests.txt`.
- Focused integration tests passed, 7 total: `bundle://proof/SB08/transcripts/focused-integration-tests.txt`.
- Focused component tests passed, 8 total: `bundle://proof/SB08/transcripts/focused-component-tests.txt`.
- MainLayout component tests passed, 7 total: `bundle://proof/SB08/transcripts/main-layout-component-tests-rerun.txt`.
- Playwright runtime/pending activation proof passed, 1 total: `bundle://proof/SB02/transcripts/playwright-runtime-pending-switch-rerun.txt`.
- EF pending model changes check passed: `bundle://proof/SB08/transcripts/ef-pending-model-changes.txt`.

## PostgreSQL concurrency proof

Automation delivery dispatch now claims `EnvelopeId`, groups by envelope, and processes groups through bounded parallelism. Process outbox and connector outbox claim enough data to build partition keys, then process partitions concurrently with conservative max parallelism. Deterministic proof is in `bundle://proof/SB05/transcripts/bounded-parallelism-source-audit.txt` and `bundle://proof/SB08/transcripts/focused-integration-tests.txt`. Benchmark limitations are documented in `bundle://proof/SB08/benchmark-report.md`.

## Canonical runtime DB proof

Runtime state comes from the canonical runtime accessor, while persisted profile activation is pending restart state. Unit, managed-files, component, and Playwright transcripts prove the UI/API no longer labels pending activation as running runtime state.

## Stale claim proof

Process dispatch renewal failure now stops mutation work. Artifact projection and step transitions verify a matching unexpired durable claim token. Source proof is in `bundle://proof/SB06/transcripts/dispatch-claim-source-audit.txt`; focused integration proof is in `bundle://proof/SB08/transcripts/focused-integration-tests.txt`.

## Residual risks

- `git fetch origin` failed with SSH public key authentication. Existing `origin/development` ref was present and proven as ancestor of HEAD.
- Full integration suite timed out at 30 minutes and encountered PostgreSQL auth failures in broad database transfer tests. Focused integration tests for this bundle passed.
- Full component suite timed out at 30 minutes after earlier stale-label failures. The touched component suites passed after fixes.
- Existing EF Core relational assembly conflict warnings remain in build/test transcripts.
- No screenshot artifact was generated for this bundle; Playwright browser proof is assertion/transcript based.

## Merge recommendation

Merge recommendation: cautiously merge after the PostgreSQL credentials used by the broad integration suite are corrected and the broad component/integration suites are rerun in CI. The implementation itself satisfies the bundle requirements with the caveats above recorded rather than hidden.
