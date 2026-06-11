# SB02 Semantic Invariants

## Project Structure Launch Completes With Durable Readback

- Invariant ID: `SB02_INV_001`
- Source raw note: determine whether process launching/execution works from product surfaces before release closure.
- Expected behavior: A user can add the `Business plan development` template to a project, publish it, link it to a project-structure node, start it from that node, and observe a completed process run with completed/skipped steps, artifact records, completed outbox receipts, and succeeded execution runs.
- Disallowed shallow implementation: creating a run or launch plan without proving automation completion, durable step outcomes, artifact readback, outbox completion, and browser-visible completed evidence.
- Failing-first proof: `bundle://proof/SB02/transcripts/failing-first-source-assertion.txt` shows `HEAD` lacked the SB02 deterministic completion/readback proof markers.
- Passing test: `bundle://proof/SB02/transcripts/focused-playwright-test.txt` proves the focused Playwright test exits 0.
- Changed source files:
  - `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs` after SHA-256 `db3f59b5c6a7296839864674bed77369f54665651a4535aaf7551273e802194a`
  - `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs` after SHA-256 `3c115b8e54d6fb520c3d2ad411738f3ad920362990980f1e542c81ea7f0118c6`
  - `repo://tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs` after SHA-256 `bf86bd9becc3f335703e8ad0699dabab0e6f0ddaebd7b4a5b9d0ca75770626cd`
- Browser assertions: `bundle://proof/SB02/transcripts/screenshot-inventory.txt` hashes eight screenshots, including completed summary, Evidence tab artifact readback, and completed steps dialog.
- API assertions: The passing Playwright test reads `/api/processes/runs/{runId}` with outbox, artifact, step, and execution-run records; it asserts completed run status, managed artifact paths, completed outbox rows, and succeeded execution runs.
- Red-team negative case: A run that remains `Active`, `Blocked`, lacks artifacts, lacks outbox records, or lacks succeeded execution runs times out or fails before the test can capture the completed browser proof.
- Downstream dependency check: SB03 can rely on SB02 for product-surface launch-to-runtime completion; SB06 can cite SB02 as browser evidence but must still run final bundle closure checks.

## Production Behavior Artifact Matrix

| Signal or record | Producer | Consumer | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Launch plan role selections | `SelectSb02ProcessMockLaunchCandidatesAsync` through `ProcessesService.SelectLaunchTechnicalAgentAsync` | Launch execution path | Source assertions verify explicit `business-strategist`, `financial-strategist`, and `marketing-specialist` process-mock role binding before browser Start. | Missing role binding previously allowed a blocked run on `Review product evidence`; the test now fails if a role is unresolved or requires provisioning. |
| Completed process run | Process runtime automation and outbox dispatch | Runs tab and process run API | Passing transcript and screenshot 06 prove `Completed` status and `7 of 8 steps / 0 gaps`. | The completion wait fails unless the run is completed and all outbox records are completed. |
| Step outcomes | Runtime finalizer and process branch outcome logic | Run steps dialog and API step list | Passing test asserts each step is completed or skipped; screenshot 08 captures the completed steps dialog. | Source assertions reject manual transition or dispatch suppression in the SB02 proof path. |
| Artifact readback | Process mock artifact projection and artifact ledger | Evidence tab and process run API | Passing test asserts managed artifact paths through API; screenshot 07 captures satisfied artifact obligations and artifact record IDs. | The test fails if API artifacts are empty or lack managed storage paths, or if the Evidence tab lacks artifact record linkage. |
| Outbox receipts | `ProcessOutboxService.ProcessPendingAsync` and production dispatch records | API detail DTO and drain helper | Passing test asserts outbox records exist and all have `CompletedAtUtc`. | Dead-lettered or failed records call `BuildSb02RunDiagnosticsAsync` and fail the test with step/outbox diagnostics. |
| Execution runs | Process mock AgentFramework execution | API detail DTO | Passing test asserts all execution runs completed, succeeded, and have completion timestamps. | Baseline DTO lacked `ExecutionRuns`, and the test now fails if execution readback is empty. |
