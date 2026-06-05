# SB40 Semantic Invariants

- Invariant ID: SB40-INV-001
- Source raw note: Preserve execution/retry/provider/no-progress behavior while slimming the execution loop and staging a module-local attempt-loop facade.
- Expected behavior: The execution loop keeps the same recovered/concurrent/launch/failure/provider/no-progress order; historical carried-proof loading preserves the `ProcessExecutionRunQueryBuilder.ForCandidate` query and descending terminal-run detail order; post-attempt facts still feed completion, retry, recovery, and final outcomes; provider repair remains helper-owned; `Execution.cs` remains below the 520-line target.
- Disallowed shallow implementation: A helper that takes over route/finalizer ownership, changes retry counts, changes provider fallback behavior, changes no-progress compression, changes historical carried-proof ordering, or leaves the execution partial above 520 lines is rejected.
- Failing-first test: N/A - process non-production refactor with no behavior change; bundle://proof/SB40/transcripts/focused-execution-loop-tests.txt proves execution/retry/provider/no-progress parity still passes.
- Passing test: bundle://proof/SB40/transcripts/focused-execution-loop-tests.txt.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionAttemptLoopFacade.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessHistoricalCarriedProofQueryCoordinator.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionPostAttemptFactsBuilder.cs, and coordinator helpers listed in bundle://proof/SB40/manifest.md.
- Production assertions: bundle://proof/SB40/transcripts/source-assertions-and-scans.txt proves facade/coordinator ownership, line-count target, no Core/driver tokens, and no stubs.
- Red-team negative case: bundle://proof/SB40/transcripts/source-assertions-and-scans.txt scans for route-order tokens, Process Core, driver API, TODO, NotImplementedException, default-return stubs, and line-count regression.
- Downstream dependency check: SB41-SB44 may proceed because the execution loop has a stable facade seam and focused parity proof.
