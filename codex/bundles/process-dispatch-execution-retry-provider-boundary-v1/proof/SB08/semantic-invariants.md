# SB08 Semantic Invariants

- Invariant ID: SB08-INV-001
- Source raw note: Preserve original functionality while isolating response text and active execution outcome behavior.
- Expected behavior: Recovered response text still prefers the latest assistant message, preferred response text still favors structured result summary or recovered structured assistant text for governed steps, and observed active executions still return an InProgress outcome with the same attempt number and no selected branch.
- Disallowed shallow implementation: Moving methods to helper files while changing governed response priority, ignoring serialized or chat session recovery text, changing active-run completion reason text, or returning a terminal outcome for active runs is rejected.
- Failing-first test: N/A - process non-production refactor with no behavior change; bundle://proof/SB08/transcripts/focused-response-active-tests.txt proves the preserved behavior.
- Passing test: bundle://proof/SB08/transcripts/focused-response-active-tests.txt.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionResponseTextResolver.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessObservedExecutionOutcomeBuilder.cs, and dispatcher wrapper files listed in bundle://proof/SB08/manifest.md.
- Production assertions: bundle://proof/SB08/transcripts/source-assertions-and-scans.txt proves wrappers delegate to helper files, line counts moved, no Core/driver tokens exist, and no stubs were introduced.
- Red-team negative case: bundle://proof/SB08/transcripts/source-assertions-and-scans.txt scans for forbidden Core/driver/stub/viewport artifacts and validates helper delegation.
- Downstream dependency check: SB09-SB12 may proceed because response selection and observed-active outcome behavior are covered by focused integration tests and source assertions.
