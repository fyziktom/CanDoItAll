# SB12 Semantic Invariants

- Invariant ID: SB12-INV-001
- Source raw note: Preserve original functionality while isolating recovered and concurrent execution adoption.
- Expected behavior: Execution-run queries still use process run id, process step id, and take 20; recoverable execution adoption still fetches detail, response text, and chat session id; concurrent adoption still chooses only current-attempt active automation runs and preserves the two-poll terminal check.
- Disallowed shallow implementation: A helper that lists the wrong execution scope, adopts previous-attempt runs, ignores session-busy collisions, loses recovered response text, or skips the second detail poll is rejected.
- Failing-first test: N/A - process non-production refactor with no behavior change; bundle://proof/SB12/transcripts/focused-adoption-selection-tests.txt proves the current-attempt and session-busy selection semantics.
- Passing test: bundle://proof/SB12/transcripts/focused-adoption-selection-tests.txt.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionRunQueryBuilder.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRecoveredExecutionAdoptionCoordinator.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessConcurrentExecutionAdoptionCoordinator.cs, and dispatcher wrappers listed in bundle://proof/SB12/manifest.md.
- Production assertions: bundle://proof/SB12/transcripts/source-assertions-and-scans.txt proves helper existence, dispatcher delegation, line-count movement, no Core/driver tokens, and no stubs.
- Red-team negative case: bundle://proof/SB12/transcripts/source-assertions-and-scans.txt scans for forbidden Core/driver/stub/viewport artifacts and validates adoption delegation.
- Downstream dependency check: SB13-SB16 may proceed because launch/failure normalization now depends on explicit adoption snapshots covered by focused tests and source assertions.
