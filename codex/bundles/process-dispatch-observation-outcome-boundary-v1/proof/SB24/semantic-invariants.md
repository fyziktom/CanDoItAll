# SB24 Semantic Invariants

- Invariant ID: SB24-OBSOUTCOME-001
- Source raw note: Continue smaller dispatcher isolation steps while preserving original behavior and avoiding premature Process Core or production driver APIs.
- Expected behavior: Existing dispatch behavior remains stable while session-state observations, execution-log observations, browser-output facts, and declared outcome parsing move into module-local helpers.
- Disallowed shallow implementation: Leaving all parsing and declared outcome rule logic in ProcessRunAutomationDispatchService.ToolValidation.cs while only adding unused helper scaffolding.
- Failing-first test: N/A - process/non-production refactor proof; malformed session JSON and legacy markdown declared-outcome rejection are adversarial negative cases in bundle://proof/SB24/transcripts/passing-tests.md.
- Passing test: ProcessAutomationObservationTests.*, ResolveSuccessfulSessionToolOutputFiles_returns_browser_filenames_for_successful_calls, and TryResolveDeclaredStepOutcome_* in bundle://proof/SB24/transcripts/passing-tests.md.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationSessionObservation.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionLogObservation.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationObservationSnapshot.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDeclaredStepOutcomeRules.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs.
- Production assertions: dispatcher wrappers now call module-local helpers; ToolValidation.cs is 793 lines; no Process Core, production driver API, UI, or prohibited viewport proof path was introduced.
- Red-team negative case: malformed session JSON returns empty observations, legacy markdown declared outcome is rejected, failed execution-log entries are ignored, and untrusted internal MAF logs are not accepted.
- Downstream dependency check: architecture guardrail tests and source assertions in bundle://proof/SB24/transcripts/source-assertions.md support continuing downstream without Process Core/driver/API drift.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| No added production behavior artifact for SB24 | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationSessionObservation.cs and repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDeclaredStepOutcomeRules.cs normalize existing dispatcher evidence | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs wrappers consume helpers; tests in bundle://proof/SB24/transcripts/passing-tests.md cover parity | bundle://proof/SB24/transcripts/source-assertions.md records line count and module-local boundary; no production API lifecycle was added | bundle://proof/SB24/transcripts/anti-stub-audit.md records no stub, no Process Core, no driver API, and no UI/prohibited viewport drift |
