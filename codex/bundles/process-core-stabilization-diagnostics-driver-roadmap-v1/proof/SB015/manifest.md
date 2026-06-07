# SB015 Proof Manifest

## Scope
- Subbundle: `SB015 - Gate E - transition intent parity`
- Objective: prove exact transition shape parity for start/block/mirror cases.

## Changed Sources
- `repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchTransitionIntentRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessTransitionIntentAdapters.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchStartTransitionPlanner.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessLifecycleRules.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Command Transcripts
- Build: `bundle://proof/SB015/transcripts/build.txt`
- Architecture transition-intent tests: `bundle://proof/SB015/transcripts/architecture-transition-intent-tests.txt`
- Focused dispatch integration tests: `bundle://proof/SB015/transcripts/process-dispatch-transition-intent-integration-tests.txt`
- Changed-file hashes: `bundle://proof/SB015/transcripts/changed-file-hashes.txt`
- Source assertions: `bundle://proof/SB015/transcripts/source-assertions.txt`
- Core transition forbidden-token scan: `bundle://proof/SB015/transcripts/core-transition-forbidden-token-scan.txt`
- Anti-stub audit: `bundle://proof/SB015/transcripts/anti-stub-audit.txt`

## Results
- `dotnet build CanDoItAll.slnx --no-incremental -v:minimal` passed with three unrelated pre-existing warnings.
- `ProcessAgentExecutionBoundaryArchitectureTests` passed: 87 tests.
- `ProcessRunAutomationDispatchServiceTests` passed: 536 tests.
- Failing-first: N/A for process transition intent extraction; parity tests prove the field-level transition shape is unchanged.
