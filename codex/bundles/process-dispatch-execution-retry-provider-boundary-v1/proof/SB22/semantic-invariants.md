# SB22 Semantic Invariants

- Invariant ID: SB22-INV-001
- Source raw note: Preserve original functionality while isolating post-attempt facts and retry decisions.
- Expected behavior: Missing tools, critical failures, completion status/reason, selected branch outcome, carried implementation proof, retry eligibility, recoverable failed-run eligibility, and retry reason ordering remain unchanged after extraction.
- Disallowed shallow implementation: A helper that changes max-attempt checks, pending approval handling, declared non-completed outcome handling, recoverable provider/finalizer/interruption classification, retry reason strings, or reason ordering is rejected.
- Failing-first test: N/A - process non-production refactor with no behavior change; bundle://proof/SB22/transcripts/focused-retry-decision-tests.txt proves retry decision parity still passes.
- Passing test: bundle://proof/SB22/transcripts/focused-retry-decision-tests.txt.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionPostAttemptFacts.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessIncompleteSuccessfulRunRetryRules.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRecoverableFailedRunRetryRules.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionRetryReasonAggregator.cs, and dispatcher wrappers listed in bundle://proof/SB22/manifest.md.
- Production assertions: bundle://proof/SB22/transcripts/source-assertions-and-scans.txt proves helper delegation, line-count movement, no Core/driver tokens, and no stubs.
- Red-team negative case: bundle://proof/SB22/transcripts/source-assertions-and-scans.txt scans the moved helper surface for Process Core, driver API, TODO, NotImplementedException, and default-return stubs.
- Downstream dependency check: SB23-SB28 may proceed because retry reasons and retry decisions now have stable helper-owned entry points.
