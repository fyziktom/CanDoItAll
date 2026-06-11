# SB02 Proof Manifest

- Subbundle: `SB02`
- Status: `Completed`
- Owned requirements: `REQ-002`
- Raw notes: prove process launch/execution works from user-facing project/project-structure surfaces, not only internal service tests.
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`
- Bundle start SHA: `430496c5e7217a847e9172dcc0c2fba57f75f75c`

## Changed File Hashes

| Path | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs` | `15a9b0aa071373bbb1871f9e6b1d1338d11183ce838d9235496e13b21e6c6126` | `db3f59b5c6a7296839864674bed77369f54665651a4535aaf7551273e802194a` |
| `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs` | `5f362db08b13bd114ce951952b5745a511c5b690dedf88f367ca588cd15910fe` | `3c115b8e54d6fb520c3d2ad411738f3ad920362990980f1e542c81ea7f0118c6` |
| `repo://tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs` | `79f32ac942a397a5aca2d404b9f943f205293eb41a326a9f54389d192a4db5f0` | `bf86bd9becc3f335703e8ad0699dabab0e6f0ddaebd7b4a5b9d0ca75770626cd` |

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-source-assertion.txt`
- Passing Playwright transcript: `bundle://proof/SB02/transcripts/focused-playwright-test.txt`
- Source assertion transcript: `bundle://proof/SB02/transcripts/source-assertions.txt`
- Browser screenshot inventory: `bundle://proof/SB02/transcripts/screenshot-inventory.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`

## Browser Artifacts

- `bundle://proof/SB02/screenshots/01-project-template-selected-large-desktop.png`
- `bundle://proof/SB02/screenshots/02-project-template-linked-structure-large-desktop.png`
- `bundle://proof/SB02/screenshots/03-project-structure-start-confirm-large-desktop.png`
- `bundle://proof/SB02/screenshots/04-project-structure-assignment-review-large-desktop.png`
- `bundle://proof/SB02/screenshots/05-project-structure-assignment-ready-large-desktop.png`
- `bundle://proof/SB02/screenshots/06-project-run-completed-summary-large-desktop.png`
- `bundle://proof/SB02/screenshots/07-project-run-artifacts-readback-large-desktop.png`
- `bundle://proof/SB02/screenshots/08-project-run-completed-steps-large-desktop.png`

## Semantic Adequacy

- Test name: `Project_structure_process_template_launch_SB02_INV_001_launches_approved_template_from_structure_context_and_reads_back_run`
- Invariant ID: `SB02_INV_001`
- Shallow-pass trap: a browser test can prove only that a run was created, while automation never drains, steps remain pending or blocked, and artifacts/outbox/execution records are unverified.
- Adversarial negative proof: `bundle://proof/SB02/transcripts/failing-first-source-assertion.txt` records that baseline `HEAD` lacked the deterministic launch-to-completed-run markers, outbox drain, artifact/readback screenshot, and outbox readback DTO.
- Semantic positive proof: `bundle://proof/SB02/transcripts/focused-playwright-test.txt` exits 0 with the focused Playwright test passing.
- Browser proof: `bundle://proof/SB02/transcripts/screenshot-inventory.txt` hashes eight 1900x1200 screenshots covering template selection, project-structure linkage, start confirmation, assignment review, completed run summary, evidence/artifact readback, and completed steps dialog.
- Source assertion proof: `bundle://proof/SB02/transcripts/source-assertions.txt` verifies process-mock role selection, production outbox drain, completed API readback, outbox/execution DTO fields, artifact screenshot, and no `SuppressAutomationDispatch=true` in the SB02 launch proof.
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt` reports no stub markers and scopes the pre-existing suppressed-dispatch smoke path outside the SB02 representative proof.

## Production Behavior Artifact Matrix

| Signal or record | Producer | Consumer | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Project launch plan | Browser project-structure start flow plus `ProcessesService` launch plan APIs | Project process workspace and launch execution API | Screenshots 03-05 and source assertions show the user-visible start flow creates a draft launch plan and binds runnable process-mock technical agents before the browser clicks Start. | Failing-first transcript shows baseline lacked the deterministic SB02 launch binding proof. |
| Process run status `Completed` | Production process runtime/outbox automation | API readback and runs tab selected-run summary | Passing Playwright transcript asserts `ProcessRunStatus.Completed`; screenshot 06 shows `Completed / 7 of 8 steps / 0 gaps`. | Last known pre-fix run blocked on `Review product evidence`; source assertions reject run-created-only proof. |
| Step run completion/skips | Runtime automation finalizer and branch outcome handling | API readback and completed steps dialog | Passing test asserts every step is completed or skipped; screenshot 08 shows the completed steps dialog. | Source assertions forbid manual transition helper markers in the SB02 launch proof. |
| Artifact records and expectation readback | Process mock automation artifact projection/finalizer | API readback and Evidence tab | Passing test asserts artifacts include managed storage paths; screenshot 07 shows satisfied artifact obligations and artifact record IDs. | Failing-first transcript shows baseline lacked the artifact/readback screenshot marker. |
| Outbox receipts | Production process outbox service | API readback and deterministic drain helper | Passing test drains pending outbox through `ProcessOutboxService.ProcessPendingAsync` and asserts every outbox record is completed. | Source assertions verify no `SuppressAutomationDispatch=true` in the SB02 proof path. |
| Execution runs | AgentFramework/process mock runtime | API readback | Passing test asserts execution runs are `Completed`, `Succeeded`, and have completion timestamps. | Failing-first transcript shows baseline DTO did not expose `ExecutionRuns` for the Playwright proof. |

## Closure Decision

- Entry gate: Passed because SB01 was completed and prepared-stage bundle validation had already passed.
- Closure gate: Passed after focused Playwright proof, browser screenshots, API readback assertions, source assertions, failing-first source proof, and anti-stub audit.
- Progression decision: SB03 may proceed; SB02 proves the user-visible project/project-structure launch flow reaches completed runtime execution with durable readback.
