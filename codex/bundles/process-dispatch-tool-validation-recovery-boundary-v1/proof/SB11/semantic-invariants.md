# SB11 Semantic Invariants

- Invariant ID: `SB11-COMPLETION-DECISION-WRAPPER`
- Source raw note: Preserve original functions while isolating only safe completion decision logic.
- Expected behavior: Non-completed runs, pending approvals, and failed run outcomes resolve through a local typed rule without moving final transitions.
- Disallowed shallow implementation: Marking a step complete from the helper or bypassing artifact, branch, or declared-outcome checks.
- Failing-first test: N/A - process refactor with preserved public behavior; existing completion negative fixtures cover rejected outcomes.
- Passing test: `bundle://proof/SB12/transcripts/completion-critical-parity.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCompletionDecisionRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- Production assertions: The helper returns only a typed decision; dispatcher keeps orchestration and final transitions.
- Red-team negative case: Failed provider outcomes, branch outcomes, and governed step checks stay covered by completion parity tests.
- Downstream dependency check: SB12 Gate C validates completion behavior before recovery retry movement.
