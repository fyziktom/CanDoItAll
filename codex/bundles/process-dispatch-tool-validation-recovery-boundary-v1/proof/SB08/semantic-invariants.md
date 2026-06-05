# SB08 Semantic Invariants

- Invariant ID: `SB08-REQUIRED-TOOL-PARITY`
- Source raw note: Continue small dispatcher isolation and preserve original functions.
- Expected behavior: Required-tool parity holds across current receipts, prior attempts, process mock satisfaction, dotnet scaffold equivalence, browser proof rules, and implementation proof filters.
- Disallowed shallow implementation: Returning only current successful receipts or ignoring current-attempt-only implementation/browser tools.
- Failing-first test: N/A - process parity gate; no new external behavior was added in SB08.
- Passing test: `bundle://proof/SB08/transcripts/required-tool-parity.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRequiredToolValidationRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- Production assertions: Normalized tool names and required-tool outputs remain ordinal and unchanged.
- Red-team negative case: Negated tool references, stale proof, and missing scaffold receipts stay rejected by tests.
- Downstream dependency check: SB09-SB13 ran only after Gate B proof passed.
