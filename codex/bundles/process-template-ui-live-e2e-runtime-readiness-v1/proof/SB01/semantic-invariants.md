# SB01 Semantic Invariants

## Explicit Diff Baseline
- Invariant ID: `SB01_INV_006`
- Source raw note: "Review the real code and real tests, not only the bundle report" and preserve the code-first ratio gate for this bundle.
- Expected behavior: Final ratio proof must be calculated from an explicit hexadecimal bundle start SHA with `git diff --numstat <start-sha>...HEAD`, not from an implicit branch name or stale previous-bundle baseline.
- Disallowed shallow implementation: A report-only ratio calculation or branch-name baseline such as `origin/main` that can silently drift.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt` proves the `HEAD` baseline did not contain `SB01_INV_006`.
- Passing test: `bundle://proof/SB01/transcripts/focused-test.txt` proves `Process_runtime_host_codefirst_SB01_INV_006_numstat_command_requires_explicit_current_bundle_start_sha` passes.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs` after SHA-256 `a7df1cb1293a4f7a952045c805677a01f96526ba630fd4aae78415634a56702e`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt` shows `BuildNumstatArguments` and the explicit SHA guard.
- Red-team negative case: `BuildNumstatArguments` rejects an empty SHA and `origin/main`, preventing implicit or branch-based ratio proof.
- Downstream dependency check: SB02-SB08 may proceed only with final ratio proof from an explicit bundle start SHA and the SB01 guard suite green.

## Production Dispatch Citation
- Invariant ID: `SB01_INV_007`
- Source raw note: "Determine whether process execution works again like before" without replacing long-running E2E proof with helper-only simulation.
- Expected behavior: Representative long-running template E2E tests must call the shared automation helper and the helper must exercise launch plan creation, agent selection, approval, execution, outbox drain, and execution-run readback.
- Disallowed shallow implementation: A test that manually completes steps with `SuppressAutomationDispatch = true` or only asserts helper method existence.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt` proves the `HEAD` baseline did not contain `SB01_INV_007`.
- Passing test: `bundle://proof/SB01/transcripts/focused-test.txt` proves `Process_runtime_host_codefirst_SB01_INV_007_long_running_template_e2e_proof_cites_production_dispatch_path` passes.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs` after SHA-256 `a7df1cb1293a4f7a952045c805677a01f96526ba630fd4aae78415634a56702e`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt` shows `ProductionAutomationPathTokens`, `Process_runtime_host_codefirst_SB01_INV_007`, and `ProcessDryRunExecutionPipeline` source coverage.
- Red-team negative case: The guard extracts the SB03/SB04 E2E method bodies and rejects `SuppressAutomationDispatch = true` inside those long-running proof tests.
- Downstream dependency check: SB03 and SB04 cannot claim automation-path proof if their representative tests stop citing the production launch/approval/dispatch/readback path.
