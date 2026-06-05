# SB12 Semantic Invariants

- Invariant ID: `SB12-COMPLETION-CRITICAL-PARITY`
- Source raw note: Preserve original functions and continue small dispatcher isolation through Gate C.
- Expected behavior: Critical failures, blocker summaries, completion decisions, process mock branches, and failed build/test evidence remain behaviorally compatible.
- Disallowed shallow implementation: Ignoring failed workspace build receipts, branch outcomes, governed outcomes, or blocker summaries.
- Failing-first test: N/A - process parity gate; existing integration negative fixtures cover rejected completion and critical-failure cases.
- Passing test: `bundle://proof/SB12/transcripts/completion-critical-parity.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCriticalToolFailureRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCompletionBlockerRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCompletionDecisionRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- Production assertions: Rule helpers remain pure and local; dispatcher keeps artifact validation, persistence, and final state transitions.
- Red-team negative case: Completion parity tests retain rejected branch, failed tool, and missing proof cases.
- Downstream dependency check: SB13 recovery retry fact extraction ran only after Gate C passed.
