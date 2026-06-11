# SB01 Semantic Invariants

## Conservative Fallback Blocker
- Invariant ID: `SB01_INV_009`
- Source raw note: "Review real code and tests" and produce a release decision without hiding the previous code-first ratio blocker.
- Expected behavior: A conservative `HEAD` worktree fallback can only support a blocked release decision; it cannot support merge-ready closure.
- Disallowed shallow implementation: A report that says merge-ready while admitting `CommandPolicy: conservative worktree fallback`.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt` proves `HEAD` did not contain this fallback-policy guard before SB01.
- Passing test: `bundle://proof/SB01/transcripts/focused-test.txt` proves `Process_runtime_host_codefirst_SB01_INV_009_ratio_report_rejects_conservative_head_fallback_unless_blocked` passes.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs` after SHA-256 `fc5ae9e6479b6f7fadd11bf0afb4539b40cb78c704b26db0ad150bda6490ea58`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt` shows `ProcessRuntimeHostCodeFirstClosureReport` rejects conservative fallback unless the decision is blocked.
- Red-team negative case: The test feeds a merge-ready decision with conservative fallback text and requires `IsPolicyConsistent` to be false.
- Downstream dependency check: SB06 cannot claim merge readiness from a conservative fallback; it must either use explicit start-SHA ratio proof or remain blocked.

## Worktree-Inclusive Explicit Baseline
- Invariant ID: `SB01_INV_010`
- Source raw note: "get process launching/execution working again before further Process Core extraction" with final closure based on real code/test changes, not stale report math.
- Expected behavior: A Codex worktree ratio command must use the explicit bundle-start SHA and include uncommitted changes with `git diff --numstat <start-sha>`.
- Disallowed shallow implementation: A commit-only `git diff --numstat <start-sha>...HEAD` transcript used as the only ratio proof while implementation remains uncommitted.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt` proves `HEAD` did not contain this worktree-inclusive guard before SB01.
- Passing test: `bundle://proof/SB01/transcripts/focused-test.txt` proves `Process_runtime_host_codefirst_SB01_INV_010_worktree_numstat_command_requires_explicit_start_sha` passes.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs` after SHA-256 `fc5ae9e6479b6f7fadd11bf0afb4539b40cb78c704b26db0ad150bda6490ea58`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt` shows `BuildWorktreeNumstatArguments` and `ValidateExplicitStartSha` reject empty, `HEAD`, and branch-name baselines.
- Red-team negative case: The test sends `HEAD` and `origin/main` to the worktree command builder and requires `ArgumentException`.
- Downstream dependency check: SB06 must rerun final ratio with explicit start-SHA proof that includes the actual Codex worktree or committed implementation changes.
