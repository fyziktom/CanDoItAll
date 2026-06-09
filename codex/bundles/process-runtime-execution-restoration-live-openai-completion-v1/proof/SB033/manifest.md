# SB033 Proof Manifest

## Status
Completed.

## Objective
Gate K: prove project-structure process output and run navigation end to end.

## Owned Requirements And Notes
- Requirement IDs: REQ-001, REQ-002, REQ-003, REQ-004, REQ-015 project-structure output subset.
- Critical invariant contract: `bundle://proof/SB033/semantic-invariants.md`
- Downstream dependency: SB034-SB036 manager diagnostics may start after project-structure output projection and run navigation are source-backed.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureProcesses.cs` | `ed674aa153c96c72918416e33ec0b3d4f4b6dadc291354253f68e808921f74b6` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/reviews/01-execution-report.md` | `cb1b4ff11ff47473194753f35c8cfd0635e31ec5e1b284604f1b329ce3aee4b2` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB031/README.md` | `7425ed1c1e0aa278d21246ada80f6cebd2427509f2079e5b4c19e13dc5cb7fe0` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB032/README.md` | `281e297928ba4dc0596f4cb739df39bade02d3c6b1dc0cbab1e6b442840a6199` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB033/README.md` | `f91f8eb25deabb17f3749597d3d5e28aba5338b2c9897c73c28af0579f62fce7` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB031/project-structure-output-run-navigation-proof.md` | `e7aceab6c5b44b10ad87fefc3e4cf0d8c34d9602bdc7fa8d39bcf2b310db76f1` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB032/project-structure-managed-output-artifact-proof.md` | `7e8aedc8a808b531c27c28b5900f5497b615a7d57179d600d219bab83a601428` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB033/transcripts/project-structure-run-output-test.txt` | `3b77b8c7b8aa87b36285d99410755014346a1813fc9f551bfa077c9b28521a2e` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB033/transcripts/source-assertions.txt` | `c39cf9e7adae4646b28beb89c10808155c308dd24c18533969e502bffd534ffc` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB033/transcripts/no-transient-bundle-path-scan.txt` | `9263c3a4607b94d0c96c7fbb521f9dce5a02534d201544c9ddf79805093439cc` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB033/transcripts/anti-stub-and-runtime-host-drift-scan.txt` | `1bc5bddff7205e613465263e35fa2a4dba70a63b64fc0052f8851d248a1ebbe4` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB033/red-team/shallow-projection-proof-rejected.md` | `92b134d632362bd7bb3d4d8f55fcdc3d5661f3515fad5cba15e1b1a58ba18a42` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB033/SB033-project-structure-run-output.trx` | `6812fd17990f65c4e52566d6692318e10147e949f6199dff3bede66d99c284bb` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB033/screenshots/01-structure-run-output-node-large-desktop.png` | `70d0e8415f08727ec8d139db7a6b53d6fe081fc6276ae05d38fd53e3b9ce3d8d` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB033/screenshots/02-run-output-quick-actions-large-desktop.png` | `67140fc7a286f5da3ebc0d94b4706be2a8031e9284727c5ae74ace66752b7baf` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB033/screenshots/03-run-output-process-workspace-before-history-wait-large-desktop.png` | `cc60ee96dec281e8fb49786c08c8c1ab4606943dc89f980e07bebcc64a28231a` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB033/screenshots/03-run-output-process-workspace-large-desktop.png` | `0ab5f5e9c4d9ca9a1cb8b5cda0cbc71f9e622180c0f02c37a3fbf06d40414878` |

## Command Transcripts
- Playwright/UI proof: `bundle://proof/SB033/transcripts/project-structure-run-output-test.txt`
- Source assertions: `bundle://proof/SB033/transcripts/source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB033/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB033/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Red-team shallow projection proof rejection: `bundle://proof/SB033/red-team/shallow-projection-proof-rejected.md`

## Browser Validation Log
| Route | Viewport | Actions | Assertions | Screenshots |
| --- | --- | --- | --- | --- |
| `/projects/{projectId}/structure` to `/projects/{projectId}/processes?processId={definitionId}&runId={runId}` | `1900x1200` | Create project/node; create/publish/link process; start process from node; record managed output artifact; open projected output quick action | `run-started`; `process-run-output:` projected node; parent `process-run:{runId}`; popup URL preserves project/process/run; selected run summary includes source work item | `bundle://proof/SB033/screenshots/01-structure-run-output-node-large-desktop.png`; `bundle://proof/SB033/screenshots/02-run-output-quick-actions-large-desktop.png`; `bundle://proof/SB033/screenshots/03-run-output-process-workspace-large-desktop.png` |

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Project-structure started run | `/api/project-structure/projects/{projectId}/nodes/{nodeId}/process/start` | Process runtime and project structure | Starts a real process run scoped to the project and source node | Red-team rejects process definition projection only |
| Managed output artifact | `/api/processes/artifacts` | Project-structure output projection | Uses scoped managed path under project/run output folder | Red-team rejects artifact-free screenshots |
| Project output node | Project structure projection | Canvas quick actions | Appears as `process-run-output:` under `process-run:{runId}` | Red-team rejects non-run output nodes |
| Run navigation route | Output node quick action | Process workspace | Opens `/projects/{projectId}/processes?processId={definitionId}&runId={runId}` and selects the run | Red-team rejects route without project/process/run identity |

## Closure
- Shallow-pass trap: A fake pass could cite seeded project-structure nodes, definition projection, or screenshots without managed output and run route identity.
- Adversarial negative proof: `bundle://proof/SB033/red-team/shallow-projection-proof-rejected.md`
- Semantic positive proof: `bundle://proof/SB033/transcripts/project-structure-run-output-test.txt`
- Anti-stub audit: `bundle://proof/SB033/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Raw-note closure: Project-structure output and run navigation are source-backed by a large-desktop Playwright test with real project/process APIs and screenshots.
