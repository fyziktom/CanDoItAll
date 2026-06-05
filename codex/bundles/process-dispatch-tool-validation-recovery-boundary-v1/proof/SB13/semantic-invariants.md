# SB13 Semantic Invariants

- Invariant ID: `SB13-RECOVERY-RETRY-BOUNDARY`
- Source raw note: Prepare for future drivers without implementing them prematurely.
- Expected behavior: Recovery retry facts preserve failed tool names, missing required tool reasons, critical failure reasons, and build/test failure categories.
- Disallowed shallow implementation: Dropping failed tool names, conflating build/test failures, or moving journal persistence into a helper.
- Failing-first test: N/A - process refactor with preserved public behavior; existing retry negative fixtures cover rejected/no-progress cases.
- Passing test: `bundle://proof/SB13/transcripts/recovery-retry-parity.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRecoveryRetryDecisionRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryPackets.cs`
- Production assertions: Retry facts are pure; dispatcher still creates payloads and owns persistence.
- Red-team negative case: Retry parity tests cover missing tools, critical failures, scaffold retry, and JavaScript browser-proof retry guidance.
- Downstream dependency check: SB14-SB16 closure uses the recovery fact boundary as the final implementation cutline.
