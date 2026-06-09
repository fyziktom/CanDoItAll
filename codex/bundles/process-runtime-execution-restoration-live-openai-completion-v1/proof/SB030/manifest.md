# SB030 Proof Manifest

## Status
Completed.

## Objective
Gate J: prove run detail and recovery UI at large desktop with API readback.

## Owned Requirements And Notes
- Requirement IDs: REQ-001, REQ-002, REQ-003, REQ-004, REQ-015 run-detail/recovery subset.
- Critical invariant contract: `bundle://proof/SB030/semantic-invariants.md`
- Downstream dependency: SB031-SB033 project-structure output/navigation proof may start after browser-visible run detail and blocked recovery readback are proven.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs` | `8c01630676359678a45e4cf5cae22f21a24751f4211081b8a21cbe91fabeb6e1` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/reviews/01-execution-report.md` | `491fcc47080b1ae52fdc455efe47cd918b31583ce4dc3527ad695a13bbb91523` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB028/README.md` | `f787866ce80cdba5dcc45a4260896255ae4cea7dc454d8d7506f87fec13998ef` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB029/README.md` | `8a320572bd3f0680d8b1f81013f1d17d973c0609a8a2e1cb38e466961f224cc1` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB030/README.md` | `3a768bdeaaac7fdc93d2d1f49ac84535ecfa67238d8cd8940d89b582c2e1350c` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB028/run-detail-ui-status-step-artifact-proof.md` | `db1977e5d01ea1f322639fe5c3a5d5ceb14d5e8adf414eff9993d821460259f7` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB029/recovery-blocked-state-ui-api-readback-proof.md` | `2be48a02120e779b7175fc1ea6c67a7dad681df21d9a87ddf9e419129fe9b095` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB030/transcripts/run-detail-recovery-ui-test.txt` | `41c9a113149ca2d05fd386d6da45d771dc4558f9ea958b1d659c53470d752b04` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB030/transcripts/source-assertions.txt` | `5e94e09f88f58b43932967f0f3198fc84245fd12739695bce483777a78e4a776` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB030/transcripts/no-transient-bundle-path-scan.txt` | `fcebe12ac7285f81f1c821318d8a6a5278b751482a0cad1ba98ea6a477d4acd0` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB030/transcripts/anti-stub-and-runtime-host-drift-scan.txt` | `726351efb222916ea43854bcc68e8a5288b0379af8aa558d367c54d757c7e277` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB030/red-team/shallow-ui-only-proof-rejected.md` | `aaa8e5a39b3b0dc748f051c2459e23abb0176b56154d3afc88877c3b60e29a84` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB030/SB030-run-detail-recovery-ui.trx` | `f39583275dd063b2a7f6c871006392a6185feebdba840ae7da6c329711e21001` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB030/screenshots/01-selected-run-summary-large-desktop.png` | `bdd5d62da9904f40c3681728ac4f8da9aa9e3a44f077a24d5d3f0c6f15628ca3` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB030/screenshots/02-step-recovery-diagnostics-large-desktop.png` | `71ba13411a6013bfaf1c7147e8dc72b704426f12b26c74ccf2d57df55b516868` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB030/screenshots/03-artifact-ledger-large-desktop.png` | `1feaa4d4d21bb7eb71ec0d485c4163b21b7277af67a9797b5a41dc042d9006d6` |

## Command Transcripts
- Playwright/UI/API proof: `bundle://proof/SB030/transcripts/run-detail-recovery-ui-test.txt`
- Source assertions: `bundle://proof/SB030/transcripts/source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB030/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB030/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Red-team shallow UI proof rejection: `bundle://proof/SB030/red-team/shallow-ui-only-proof-rejected.md`

## Browser Validation Log
| Route | Viewport | Actions | Assertions | Screenshots |
| --- | --- | --- | --- | --- |
| `/processes?processId={definitionId}&runId={runId}` | `1900x1200` | Create definition; publish; start run; block step; record artifact; open run detail; inspect Runs dialog and Evidence tab | API blocked/recovery/artifact readback; UI selected summary; typed recovery attributes; artifact ledger | `bundle://proof/SB030/screenshots/01-selected-run-summary-large-desktop.png`; `bundle://proof/SB030/screenshots/02-step-recovery-diagnostics-large-desktop.png`; `bundle://proof/SB030/screenshots/03-artifact-ledger-large-desktop.png` |

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Blocked process run | `/api/processes/runs/start` and step transition API | Process run detail API and UI | Persisted as `Blocked`, selected by `runId` query, rendered in selected run summary | Red-team rejects plain `/processes` load/static screenshot |
| Typed recovery state | `ProcessStepBlockCause.OwnOutput` transition through process API | Run detail API, run steps dialog | Persists `ArtifactContractUnsatisfied`, `RecoverArtifactsOnly`, and recovery options | Red-team rejects text-only proof without typed attributes |
| Artifact obligation/readback | Step artifact API with `ArtifactExpectationId` | Run detail API and Evidence tab | Persists durable artifact record and satisfies expectation ledger | Red-team rejects screenshot-only artifact claim |
| No driver hook | Source scans and public API setup | Gate J review | Uses process service HTTP routes and Playwright UI only | Runtime-host drift scan has no matches |

## Closure
- Shallow-pass trap: A fake pass could load `/processes`, cite static UI, or take screenshots without proving typed blocked recovery/readback.
- Adversarial negative proof: `bundle://proof/SB030/red-team/shallow-ui-only-proof-rejected.md`
- Semantic positive proof: `bundle://proof/SB030/transcripts/run-detail-recovery-ui-test.txt`
- Anti-stub audit: `bundle://proof/SB030/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Raw-note closure: Run detail/recovery UI is source-backed by a large-desktop Playwright test with public API setup/readback and screenshots.
