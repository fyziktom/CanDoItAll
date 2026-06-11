# SB04 Proof Manifest

## Status
- Subbundle: SB04
- Status: Completed
- Owned requirements: REQ-006, REQ-007
- Raw notes: RN-001, RN-004
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`

## Changed File Manifest
| Path | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs` | `db3f59b5c6a7296839864674bed77369f54665651a4535aaf7551273e802194a` | `5ce79272eaf506274e8881c807d7d9a0dc7bfa7e9cac2fdf7fb94481b1c5b3b0` |

## Command Transcripts
- Playwright completed-run proof: `bundle://proof/SB04/transcripts/playwright-project-structure-completed-run.txt`
- Screenshot review: `bundle://proof/SB04/transcripts/screenshot-review.txt`
- Source assertion transcript: `bundle://proof/SB04/transcripts/source-assertions.txt`
- Failing-first source assertion: `bundle://proof/SB04/transcripts/failing-first-source-assertion.txt`
- Anti-stub audit transcript: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`

## Artifact Hashes
| Artifact | SHA-256 |
| --- | --- |
| `bundle://proof/SB04/transcripts/playwright-project-structure-completed-run.txt` | `478c9f7be9428e1d2816a604ea8a3bf473cb1aa7cc53626e5ca35bf75644e5c0` |
| `bundle://proof/SB04/transcripts/screenshot-review.txt` | `4f35625f467107ecd9239e152e6706bbef59112ae6bad03ab7dbc30cb7fce1ea` |
| `bundle://proof/SB04/transcripts/source-assertions.txt` | `f66e3f04f9a193df75866d1d707bb0f7b31092688f4cd6fbcb517ff7027b2941` |
| `bundle://proof/SB04/transcripts/failing-first-source-assertion.txt` | `27ea32b98d8275bdaae6006ad37fd2ecabe622d51593e2d2c8b2aecaae211fb4` |
| `bundle://proof/SB04/transcripts/anti-stub-audit.txt` | `a3da672d75381112afe0561bcd95ff8babd4d06c6957c65203cb9753b864e7dd` |

## Screenshot Artifacts
| Screenshot | SHA-256 |
| --- | --- |
| `bundle://proof/SB04/screenshots/01-project-template-selected-large-desktop.png` | `b76bacaa86c74a74a15d5f680af3b5756251d7555853c1be8dd931a68bf97100` |
| `bundle://proof/SB04/screenshots/02-project-template-linked-structure-large-desktop.png` | `7b69089f27c132815e4dde050cc928037d7b1f18be7a55b7738bb7a6d211db72` |
| `bundle://proof/SB04/screenshots/03-project-structure-start-confirm-large-desktop.png` | `53c57b590b26345177a98abdb1aed163245254b1deab34c94a7ba32f83292e47` |
| `bundle://proof/SB04/screenshots/04-project-structure-assignment-review-large-desktop.png` | `9f56fddd065f9e319a4caefa78dd15c5baf8afeec53dd4d11622b982f27da65e` |
| `bundle://proof/SB04/screenshots/05-project-structure-assignment-ready-large-desktop.png` | `9f56fddd065f9e319a4caefa78dd15c5baf8afeec53dd4d11622b982f27da65e` |
| `bundle://proof/SB04/screenshots/06-project-run-completed-summary-large-desktop.png` | `cc38986572f23124af3b55ff2f5b5eeb2bcd38504c8a0d3be4e58c27fb5bbe51` |
| `bundle://proof/SB04/screenshots/07-project-run-artifacts-readback-large-desktop.png` | `a60dc32179f5c8f9773504f8ae8f0a8a57758eb1ec4f013eb4bca399a64122f4` |
| `bundle://proof/SB04/screenshots/08-project-run-runtime-host-readback-large-desktop.png` | `8ef7fd33a0fe4d72a6385f1f14dae6fc75e3144f29954bd5958d12fbafbe6e12` |
| `bundle://proof/SB04/screenshots/09-project-run-completed-steps-large-desktop.png` | `ba567907b8d49528640a9a8af8979fdcee583d018f58784e7dd2945111773ef4` |

## Browser Proof
- Route: `/projects/{projectId}/structure` to start, then `/projects/{projectId}/processes?processId={definitionId}&runId={runId}` for completed run readback.
- Viewport: 1900x1200.
- Result: passed.
- The completed run summary, evidence/artifact ledger, completed/skipped step dialog, and runtime-host operator readback are all assertion-backed and screenshot-backed.

## Source Assertions
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs` now asserts completed-run runtime-host operator readback on the project-structure launched run.
- `bundle://proof/SB04/transcripts/source-assertions.txt` proves the new source tokens and screenshot path are present.

## Failing-First And Passing Proof
- Failing-first: `bundle://proof/SB04/transcripts/failing-first-source-assertion.txt` proves baseline `HEAD` lacked the completed-run runtime-host readback screenshot token.
- Passing: `bundle://proof/SB04/transcripts/playwright-project-structure-completed-run.txt` exits zero for the focused Playwright proof.

## Anti-Stub Audit
- `bundle://proof/SB04/transcripts/anti-stub-audit.txt` reports no `TODO`, `NotImplemented`, or `fixture-specific` markers in the changed Playwright test file.

## Downstream Smoke
- SB05 may proceed because UI launch-to-completed-run and operator readback proof is green.
