# SB014 Proof Manifest

## Scope
- Subbundle: `SB014 - Module transition intent adapter`
- Objective: map Core transition intents/facts to module transition requests.

## Changed Sources
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessTransitionIntentAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchStartTransitionPlanner.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessLifecycleRules.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Proof
- Start-transition adapter test: `bundle://proof/SB014/transcripts/module-transition-intent-adapter-test.txt`
- Subprocess transition adapter test: `bundle://proof/SB014/transcripts/subprocess-transition-intent-adapter-test.txt`
- Critical gate integration proof: `bundle://proof/SB015/transcripts/process-dispatch-transition-intent-integration-tests.txt`
- Source assertions: `bundle://proof/SB015/transcripts/source-assertions.txt`

## Result
- `ProcessTransitionIntentAdapters` owns `ProcessStepTransitionRequest` construction for Core start-transition intents and subprocess parent transition facts.
- Start/block/mirror transition request fields preserve previous behavior.
- Claims and EF remain outside Core.
