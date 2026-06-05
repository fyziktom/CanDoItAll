# SB06 Semantic Invariants

- Invariant ID: `SB06_INV_001`
- Source raw note: RN-003.
- Expected behavior: Competing active execution selection ignores the current execution run, filters stale/manual/previous-attempt records, and selects the newest current-attempt automation run.
- Disallowed shallow implementation: Moving only method names while leaving duplicated route LINQ in the dispatcher or allowing previous-attempt runs to block the current attempt.
- Failing-first test: N/A - non-critical helper foundation; parity was proven by focused helper tests and existing wrapper tests.
- Passing test: `bundle://proof/SB06/transcripts/sb06-selection-helper-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionRunSelection.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`.
- Production assertions: `bundle://proof/SB06/source-assertions/execution-run-selection-helper.md`.
- Red-team negative case: `bundle://proof/SB06/transcripts/sb06-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB07/SB08 can target the selector without moving execution-client calls into pure helpers.

- Invariant ID: `SB06_INV_002`
- Source raw note: RN-001 and RN-003.
- Expected behavior: Stale automation runs stop blocking after the timeout unless they have pending approvals; approval-waiting runs remain blocking.
- Disallowed shallow implementation: Treating every old active run as stale and breaking pending approval ownership.
- Failing-first test: N/A - non-critical helper foundation; existing wrapper tests plus direct helper assertions cover the branch.
- Passing test: `bundle://proof/SB06/transcripts/sb06-selection-helper-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionRunSelection.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`.
- Production assertions: `bundle://proof/SB06/source-assertions/execution-run-selection-helper.md`.
- Red-team negative case: `bundle://proof/SB06/transcripts/sb06-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB08 parity proof must preserve approval-waiting blocking semantics.

- Invariant ID: `SB06_INV_003`
- Source raw note: RN-001 and RN-003.
- Expected behavior: Fresh runtime recovery skips only early in-progress recovery scans without an existing recoverable execution run, and completion transition skips remain limited to identical or non-active statuses.
- Disallowed shallow implementation: Collapsing trigger handling into broad in-progress suppression or making terminal status transitions run again.
- Failing-first test: N/A - non-critical helper foundation; existing wrapper tests plus direct helper assertions cover the branch.
- Passing test: `bundle://proof/SB06/transcripts/sb06-selection-helper-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionRunSelection.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`.
- Production assertions: `bundle://proof/SB06/source-assertions/execution-run-selection-helper.md`.
- Red-team negative case: `bundle://proof/SB06/transcripts/sb06-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB10 start-transition and fresh-skip planning must use these facts without adding hidden side effects.
