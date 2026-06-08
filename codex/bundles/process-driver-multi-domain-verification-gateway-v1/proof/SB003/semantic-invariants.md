# SB003 Semantic Invariants

## Invariant SB003_INV_001
- Invariant ID: `SB003_INV_001`
- Source raw note: `Review latest Codex work after crash using real code`; the gate must not trust report-only proof.
- Expected behavior: Gate A closes only when source reconciliation, build, focused tests, source scan, anti-stub audit, and test-debt inventory are backed by existing command transcripts or proof artifacts.
- Disallowed shallow implementation: A filled execution-report row or non-empty prose claim that says the baseline passed without citing durable `bundle://proof/...` artifacts.
- Failing-first test: `bundle://proof/SB003/transcripts/red-team-report-only-proof-rejection.txt` intentionally rejects a fake report-only proof and records `Exit code: 1`.
- Passing test: `bundle://proof/SB003/transcripts/gate-a-proof-index.txt` checks the required proof artifacts exist and records `Exit code: 0`.
- Changed source files: No production source file changed in SB003. Bundle/proof files changed: `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/subbundles/sb003-gate-a-source-backed-baseline-closure-with-no-report-only-proof/README.md` hash `0cd2ec86c2fb69f37481c40c8201fcaefb15bf52e699179600f109744440fde9`; `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/reviews/01-execution-report.md` hash `1cb51a6e994d7d31bdda4aff9e58b5e804666baf84387f0047565dd7c2e3dbaf`.
- Production assertions: The driver verification lane remains source-backed and unchanged; source hashes include `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationAlphaVerifier.cs` hash `588388f6562bde97a1104e68235d199ac52215700d7ed7e5ea645f8cb1b3cb0f` and `repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence/RuntimeEvidenceConsistencyAlphaVerifier.cs` hash `039622a1ae07d9fd337abda07fdf861621a6af31a7307ac74f3365ab3af8a4f2`.
- Red-team negative case: A fake proof sentence with no artifact paths is rejected in `bundle://proof/SB003/transcripts/red-team-report-only-proof-rejection.txt`.
- Downstream dependency check: SB004 owns stale architecture fixture paths and SB005 owns intermittent `TuningRequestServiceTests` cleanup debt before SB006 Gate B; SB003 allows only source-backed downstream work.

## Artifact Matrix
| Artifact | Role | Required signal |
| --- | --- | --- |
| `source-reconciliation.txt` | Source proof | Live branch and referenced source paths are reconciled. |
| `solution-build-no-restore.txt` | Build proof | Baseline solution build succeeds. |
| `focused-baseline-unit-tests.txt` | Focused test proof | Baseline focused tests pass. |
| `source-scan-and-anti-stub-audit.txt` | Source scan proof | Driver verification lane has no forbidden runtime/stub drift. |
| `red-team-report-only-proof-rejection.txt` | Adversarial proof | Report-only Gate A closure is rejected. |
| `gate-a-proof-index.txt` | Positive proof index | Required baseline proof artifacts exist and are source-backed. |
