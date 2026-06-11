# SB03 Semantic Invariants

## Invariant SB03_INV_001
- Invariant ID: `SB03_INV_001`
- Source raw note: Determine whether the Blazor/.NET representative process works through real automation again, not through manual step transitions.
- Expected behavior: The Blazor app delivery template runs through process-mock automation dispatch, completes the active path, records completed outbox dispatch records, maps one completed execution run to each automated active-path step, records finalizer summaries, stores required artifacts with managed-file readback, selects the `Quality accepted` branch, and skips the repair branch.
- Disallowed shallow implementation: Manually transitioning steps with `SuppressAutomationDispatch = true`, proving only imported definitions, checking only step statuses without outbox/execution-run records, or accepting artifact records without managed output files.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first-source-assertion.txt`
- Passing test: `bundle://proof/SB03/transcripts/focused-integration.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs`
- Production assertions: `AssertAutomationDispatchReadback` verifies completed outbox records, process-mock source/provider/model, run id, step-run mapping, completed execution state, and succeeded outcome. `AssertArtifact` verifies managed-storage-backed artifact records by reading real managed output files under the active test workspace.
- Red-team negative case: `bundle://proof/SB03/transcripts/suppress-dispatch-scan.txt` proves `SB03_INV_001` contains no `SuppressAutomationDispatch = true` and labels the older manual helper as a manual contract test.

## Invariant SB03_INV_002
- Invariant ID: `SB03_INV_002`
- Source raw note: Missing process-mock role mappings must fail predictably before dispatch instead of silently falling back or creating a half-configured run.
- Expected behavior: Launch candidate selection fails when the required `blazor-engineer` role is omitted from the process-mock role map, the exception names the missing role, no project run is generated, and no `dispatch-run-automation` outbox record is created for the failed launch path.
- Disallowed shallow implementation: Letting launch approval/execution proceed with a missing required role, silently selecting a fallback agent, creating a process run before validation completes, or recording a dispatch outbox item for an invalid assignment plan.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first-source-assertion.txt`
- Passing test: `bundle://proof/SB03/transcripts/focused-integration.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs`
- Production assertions: The negative test calls `ExecuteTemplateWithProcessMockAgentsAsync` with `blazor-engineer` omitted, asserts the existing support guard message, and reads the database to assert no `ProcessRun` and no `dispatch-run-automation` outbox record exist for the project.
- Downstream dependency check: SB04 and SB05 can rely on the same process-mock launch/approval/dispatch helper only if their representative tests do not suppress automation dispatch and include execution-run/artifact readback.
