# SB054 Semantic Invariants

## Gate R Invariant
- Requirement owned: `REQ-020`.
- Required behavior: closure through SB053 must have fully expanded execution rows, manifest-backed proof, transcript-backed command evidence, critical semantic/matrix artifacts, and explicit pending rows for SB054-SB060 before roadmap/final closure continues.
- Disallowed shallow implementation: report-row-only closure, manifests without transcript scans, scan-only closure without build proof, collapsed rows, missing critical matrices, or hidden final-closure blockers.
- Failing-first proof: `bundle://proof/SB054/transcripts/red-team-gate-r-report-only-closure-rejection.txt` rejects report-row-only, manifests-without-transcript-scan, scan-without-build, and collapsed-future-row closure claims.
- Passing proof: `bundle://proof/SB054/transcripts/gate-r-no-collapsed-report-only-scan.txt` verifies SB001-SB053 execution rows, manifests, transcripts, and critical matrices.
- Source proof: `bundle://proof/SB054/transcripts/gate-r-solution-build-no-restore.txt` proves the current source tree builds with 0 warnings and 0 errors.

## Reopen Conditions
- Reopen if any completed row loses a passed entry gate, closure gate, dependency check, or progression result.
- Reopen if any completed manifest loses changed-file hashes, command transcripts, source assertions, validation results, closure gate, or referenced transcript artifacts.
- Reopen if any critical gate lacks a production behavior artifact matrix or semantic artifact matrix.
- Reopen if future rows are collapsed, hidden, or treated as complete before their subbundles are executed.
- Reopen if a closure claim can pass from report text alone.

## Artifact Matrix
| Artifact | Role | Required signal |
| --- | --- | --- |
| `gate-r-solution-build-no-restore.txt` | Build proof | Solution build succeeds with 0 warnings and 0 errors. |
| `gate-r-no-collapsed-report-only-scan.txt` | Closure source proof | SB001-SB053 rows, manifests, transcripts, and matrices are complete; SB054-SB060 remain explicit pending rows. |
| `red-team-gate-r-report-only-closure-rejection.txt` | Adversarial proof | Report-only, scan-only, manifest-only, and collapsed-row closure are rejected. |
| `gate-r-proof-index.txt` | Positive proof index | Verifies Gate R build, scan, red-team, semantic invariants, upstream manifests, and secret-scan-clean proof. |
