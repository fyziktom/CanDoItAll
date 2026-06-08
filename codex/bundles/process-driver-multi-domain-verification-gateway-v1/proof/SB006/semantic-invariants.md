# SB006 Semantic Invariants

## SB006_INV_001
- Invariant ID: `SB006_INV_001`
- Source raw note: `Review latest Codex work after crash using real code`.
- Expected behavior: Gate B can close only when the solution build is clean, the full unit project has exit code 0, every remaining skipped test is explicitly quarantined with owner and reopen trigger, and downstream phases inherit an artifact-backed baseline.
- Disallowed shallow implementation: report-only status updates, non-empty diagnostics without command transcripts, fixture-only claims, hiding skipped tests, or claiming full-unit closure without a debt ledger.
- Failing-first test: `bundle://proof/SB006/transcripts/red-team-status-only-gate-b-rejection.txt` rejects status-only closure that lacks full-unit proof, owner, reopen trigger, and source/no-drift audit.
- Passing test: `bundle://proof/SB006/transcripts/gate-b-proof-index.txt` verifies the build transcript, full-unit transcript, remaining-debt ledger, upstream SB004/SB005 manifests, source audit, and red-team rejection.
- Changed source files: `repo://tools/CanDoItAll.Manager/TuningRequestService.cs`; `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- Production assertions: `TuningRequestService.SetStatusAsync` appends `events.ndjson` before exposing updated status through `_requests`, eliminating the teardown file-lock race without retries or sleeps.
- Remaining debt: exactly 21 stale historical architecture fixture tests are skipped with `HistoricalBundleFixtureQuarantineReason`, owned by SB004, and listed in `bundle://proof/SB006/remaining-debt-ledger.md`.
- Reopen trigger: remove the quarantine and restore active coverage when the historical bundle fixtures are restored, migrated into stable current-bundle fixture inputs, or replaced by equivalent source-backed architecture guards.
- Adversarial negative case: a status-only execution report row without full-unit proof and debt ownership is rejected with simulated verifier exit code 1.
- Downstream dependency check: SB007 and later phases may proceed only from the SB006 baseline artifacts; if the full-unit run fails, skip count changes without ledger update, or SB004/SB005 manifests are missing, downstream phases must reopen.

## Artifact Matrix
| Artifact | Role | Required signal |
| --- | --- | --- |
| `gate-b-solution-build-no-restore.txt` | Build proof | Solution build succeeds with 0 warnings and 0 errors. |
| `gate-b-full-unit-tests.txt` | Unit proof | Full unit project has 0 failures and known skip count. |
| `remaining-debt-ledger.md` | Debt proof | Every intentional skip has owner and reopen trigger. |
| `red-team-status-only-gate-b-rejection.txt` | Adversarial proof | Status-only Gate B closure is rejected. |
| `gate-b-proof-index.txt` | Positive proof index | Build, unit, debt, source audit, and red-team artifacts are verified. |
