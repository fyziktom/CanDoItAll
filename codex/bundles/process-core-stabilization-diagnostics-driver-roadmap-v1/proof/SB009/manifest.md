# SB009 Proof Manifest

## Scope
- Subbundle: `SB009 - Gate C - diagnostics parity proof`
- Objective: prove route and artifact diagnostics are additive only.

## Changed Sources
- `repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRoutePlanner.cs`
- `repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactExpectationMatcher.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactExpectationMatcher.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Command Transcripts
- Build: `bundle://proof/SB009/transcripts/build.txt`
- Architecture/API boundary tests: `bundle://proof/SB009/transcripts/architecture-api-and-boundary-tests.txt`
- Focused dispatch integration tests: `bundle://proof/SB009/transcripts/process-dispatch-diagnostics-integration-tests.txt`
- Changed-file hashes: `bundle://proof/SB009/transcripts/changed-file-hashes.txt`
- Source assertions: `bundle://proof/SB009/transcripts/source-assertions.txt`
- Core forbidden-token scan: `bundle://proof/SB009/transcripts/core-forbidden-token-scan.txt`
- Anti-stub audit: `bundle://proof/SB009/transcripts/anti-stub-audit.txt`

## Results
- `dotnet build CanDoItAll.slnx --no-incremental -v:minimal` passed with three unrelated pre-existing warnings.
- `ProcessAgentExecutionBoundaryArchitectureTests` passed: 85 tests.
- `ProcessRunAutomationDispatchServiceTests` passed: 535 tests.
- Failing-first: N/A for additive process diagnostics because existing behavior was intentionally preserved; adversarial negative diagnostics cases are covered in `semantic-invariants.md`.
