# SB018 Proof Manifest

## Scope
- Subbundle: `SB018 - Gate F - artifact/subprocess diagnostics proof`
- Objective: prove artifact satisfaction and subprocess mapping diagnostics are additive and behavior-preserving.

## Changed Sources
- `repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactExpectationSatisfactionRules.cs`
- `repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessSubprocessArtifactSourceResolver.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactExpectationSatisfactionAdapter.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessArtifactSourceResolver.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPlanBuilder.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Command Transcripts
- Build: `bundle://proof/SB018/transcripts/build.txt`
- Architecture/API boundary tests: `bundle://proof/SB018/transcripts/architecture-artifact-subprocess-diagnostics-tests.txt`
- Focused dispatch integration tests: `bundle://proof/SB018/transcripts/process-dispatch-artifact-subprocess-diagnostics-integration-tests.txt`
- Changed-file hashes: `bundle://proof/SB018/transcripts/changed-file-hashes.txt`
- Source assertions: `bundle://proof/SB018/transcripts/source-assertions.txt`
- Core artifact forbidden-token scan: `bundle://proof/SB018/transcripts/core-artifact-forbidden-token-scan.txt`
- Anti-stub audit: `bundle://proof/SB018/transcripts/anti-stub-audit.txt`

## Results
- `dotnet build CanDoItAll.slnx --no-incremental -v:minimal` passed with three unrelated pre-existing warnings.
- `ProcessAgentExecutionBoundaryArchitectureTests` passed: 87 tests.
- `ProcessRunAutomationDispatchServiceTests` passed: 537 tests.
- Failing-first: N/A for additive diagnostics; legacy string diagnostics and selected artifact behavior are preserved while typed reasons are added.
