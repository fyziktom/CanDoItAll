# SB013 Proof Manifest

## Scope
- Subbundle: `SB013 - Core transition intent facts`
- Objective: create pure Core transition intent facts without moving transition execution into Core.

## Changed Sources
- `repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchTransitionIntentRules.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Proof
- Focused Core transition intent architecture test: `bundle://proof/SB013/transcripts/core-transition-intent-architecture-test.txt`
- Critical gate architecture proof: `bundle://proof/SB015/transcripts/architecture-transition-intent-tests.txt`
- Core transition forbidden-token scan: `bundle://proof/SB015/transcripts/core-transition-forbidden-token-scan.txt`
- Source assertions: `bundle://proof/SB015/transcripts/source-assertions.txt`

## Result
- Core exposes `ProcessDispatchStepTransitionIntent` and `ProcessDispatchTransitionIntentRules`.
- Core does not reference `ProcessStepTransitionRequest` or transition execution.
- No production driver API was introduced.
