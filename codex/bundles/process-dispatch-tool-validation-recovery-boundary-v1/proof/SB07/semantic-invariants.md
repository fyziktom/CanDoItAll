# SB07 Semantic Invariants

- Invariant ID: `SB07-REQUIRED-TOOL-CONSUMER`
- Source raw note: Preserve original functions while continuing smaller dispatcher isolation.
- Expected behavior: Missing-required-tool consumers delegate to a typed local helper and preserve existing output names.
- Disallowed shallow implementation: Treating any prior receipt as current proof or dropping process mock and scaffold equivalence rules.
- Failing-first test: N/A - process refactor with preserved public behavior; existing integration negative fixtures cover rejected required-tool cases.
- Passing test: `bundle://proof/SB08/transcripts/required-tool-parity.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRequiredToolValidationRules.cs`
- Production assertions: Tool names, required-tool names, and carry-forward filters remain strongly typed through policy records.
- Red-team negative case: Carry-forward and current-attempt-only proof cases reject stale implementation proof.
- Downstream dependency check: SB08 Gate B parity validates this migration before completion and recovery helpers move.
