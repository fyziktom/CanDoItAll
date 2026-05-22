# SB04 Proof Manifest

## Status

- `Required during execution`

## Required Artifacts

| Artifact | Required path or rule | Status |
| --- | --- | --- |
| Regression command transcript | `proof/SB04/evidence/regression-tests.txt` | Pending |
| Clean development DB setup transcript | `proof/SB04/evidence/clean-development-db-setup.txt` | Pending |
| Fresh process run summary | `proof/SB04/evidence/fresh-process-run-summary.txt` | Pending |
| Process artifact record query | `proof/SB04/evidence/browser-artifact-record-query.txt` | Pending |
| Browser screenshot artifact | Scoped process artifact path from fresh run | Pending |
| Browser console artifact | Scoped process artifact path from fresh run | Pending |
| Browser snapshot or DOM/evaluate artifact | Scoped process artifact path from fresh run | Pending |
| Red-team fake-proof audit | `proof/SB04/evidence/fake-proof-resistance.txt` | Pending |

## Production Behavior Artifact Matrix

| Production artifact or signal | Producer | Consumer | Lifecycle proof required | Negative-test citation |
| --- | --- | --- | --- | --- |
| Clean-DB process browser evidence | Fresh process execution | User demo validation and final closure | Created during live QA browser-proof step | Detached `.playwright-mcp` evidence only cannot pass |
| Browser analytics report | Execution report | Final closure gate | Filled after browser proof and artifact queries | Missing screenshot review fails closure |

## Completion Rule

Final closure cannot pass until this manifest cites existing artifacts from a fresh run and the execution report closes every raw note.
