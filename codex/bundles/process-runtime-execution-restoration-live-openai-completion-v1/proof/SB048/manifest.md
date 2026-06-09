# SB048 Proof Manifest

## Status
Completed.

## Objective
Gate P: prove failure triage and observability for failed or blocked process runs.

## Owned Requirements And Notes
- Requirement IDs: REQ-001, REQ-002, REQ-003, REQ-004, REQ-015 failure-triage/operator-observability subset.
- Critical invariant contract: `bundle://proof/SB048/semantic-invariants.md`
- Downstream dependency: SB049-SB051 release-candidate smoke may start after failure taxonomy and operator readback are source-backed.
- Production code changes: none for SB046-SB048; existing typed source surfaces and integration tests satisfy the gate.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/reviews/01-execution-report.md` | `d4e1eba5622aed7298acf301983ad249007b72ecadd584507e9f7a93c18b44d9` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB046/README.md` | `025ccfca1fefd553cbfb684f4557a0d68135bb9ffa835fc4a12b1b21e2802ea3` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB047/README.md` | `d34fcc6933041a324f3434e255a7c33fea98e402b03962fc41dc1824d254e395` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB048/README.md` | `519430d03a81cd65d8b0688a11c9e15030dbcd2281892ed7f19214aeda935268` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB046/structured-failure-taxonomy-proof.md` | `666a5b63ea72c081d3b9eaa73ed2ea42dd91f87cffd89c3568574ce2c14cf5a9` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB047/operator-troubleshooting-readback-proof.md` | `ca49e7016233be60a94d408903a01ad70bb23b413aa04cc3d38dbc4d31666592` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB048/transcripts/failure-triage-observability-tests.txt` | `9be5f478b78dd1e23aaca9693ad2c7ac85d154d4f674f9671b82dc6da79c8c01` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB048/transcripts/source-assertions.txt` | `a89501b095e97f1ef00d5622c0b06d9dc95b6e71f31ee045d9d38a0c7e088cc6` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB048/transcripts/no-transient-bundle-path-scan.txt` | `f3b0ef11266d498382c77504a0b667b6e53ab9458deb0127d9fe3aca133f613e` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB048/transcripts/anti-stub-and-runtime-host-drift-scan.txt` | `3667c3b0cd7fb7ce346a68dd4f3ff5e3a9fd7359cb41234a8d1c871fc5c1a5d9` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB048/transcripts/production-driver-runtime-host-scan.txt` | `2f8ec703329e6481cbe17dbb5520b66d70d1707697cc43273f62ca3cb15bc243` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB048/red-team/failure-triage-shallow-proof-rejected.md` | `85f628520a685e512ca11c1f446131de1c4cd9af7b6dbff2d04e691acf243cef` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB048/semantic-invariants.md` | `9e3b07e0da2b0482a015a18f61d036115bf88005419f2f91869014d6dbb71b9e` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB048/SB048-failure-triage-observability.trx` | `e64cac663c28e33809e79cb4e1a84b7061d1a09d886e2a487ebbbf0c7e4aae61` |

## Command Transcripts
- Failure triage/observability integration tests: `bundle://proof/SB048/transcripts/failure-triage-observability-tests.txt`
- Source assertions: `bundle://proof/SB048/transcripts/source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB048/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB048/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Production driver runtime-host scan: `bundle://proof/SB048/transcripts/production-driver-runtime-host-scan.txt`
- Red-team shallow proof rejection: `bundle://proof/SB048/red-team/failure-triage-shallow-proof-rejected.md`

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Agent failure category and recovery decision | Dispatch recovery packet builder and recovery decision factory | Rework packet creation, recovery ledger, recovery context | Failed automation detail is classified into typed category/mode and reused for targeted repair or escalation | Rejects log-string-only failure proof |
| Blocked-step recovery state | `ProcessStepRunBlockState` and `ProcessRecoveryRouter` | Runtime step readback, API run detail, operator UI/read model | Blocked transition stores reason code, recovery options, next action, classification, and evidence fingerprint | Rejects generic failed status without typed recovery action |
| Run and step health view models | Runtime read model/loaders | API clients and Process Workspace operators | Health summary exposes recovery classification, actionable reason, recommended action, missing-artifact count, outbox health, and attempts | Rejects status-only readback |
| Runtime invariant diagnostics | Runtime invariant auditor and process service diagnostics | Operator troubleshooting read model | Weak artifacts, duplicate lineage, blocked recovery state, and manual transition failures surface with recommended actions and evidence keys | Rejects hidden diagnostics that are not operator-readable |
| Dead-lettered outbox health | Process outbox records/read model | Operator health, escalations, attempt timeline | Exhausted automation dispatch projects as dead-letter health with escalation and timeline entries | Rejects outbox-only proof without health projection |

## Closure
- Shallow-pass trap: a generic failed status, error log, or previous UI screenshot counted as failure observability.
- Adversarial negative proof: `bundle://proof/SB048/red-team/failure-triage-shallow-proof-rejected.md`
- Semantic positive proof: `bundle://proof/SB048/transcripts/failure-triage-observability-tests.txt`
- Anti-stub audit: `bundle://proof/SB048/transcripts/no-transient-bundle-path-scan.txt`, `bundle://proof/SB048/transcripts/anti-stub-and-runtime-host-drift-scan.txt`, and `bundle://proof/SB048/transcripts/production-driver-runtime-host-scan.txt`
- Raw-note closure: failure triage and operator observability are solved for the current process-owned runtime path; release-candidate smoke remains owned by SB049-SB051.
