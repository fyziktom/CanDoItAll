# SB008 Proof Manifest

## Scope
- Subbundle: `SB008 - Artifact match diagnostics`
- Objective: add diagnostics for strong match, kind disambiguation, no match, and ambiguous match.

## Changed Sources
- `repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactExpectationMatcher.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactExpectationMatcher.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Proof
- Focused artifact matcher diagnostics test: `bundle://proof/SB008/transcripts/artifact-match-diagnostics-tests.txt`
- Critical gate integration proof: `bundle://proof/SB009/transcripts/process-dispatch-diagnostics-integration-tests.txt`
- Core API/boundary proof: `bundle://proof/SB009/transcripts/architecture-api-and-boundary-tests.txt`
- Source assertions: `bundle://proof/SB009/transcripts/source-assertions.txt`
- Core dependency scan: `bundle://proof/SB009/transcripts/core-forbidden-token-scan.txt`

## Result
- Legacy strong-match behavior is preserved.
- Diagnostics distinguish strong match, kind disambiguation, no strong match, ambiguous kind match, and ambiguous strong match.
- Module adapter consumes the Core diagnostic result and continues returning the legacy matched artifact id.
