# SB09 Semantic Invariants

## Invariant SB09-INV-001

- Invariant ID: SB09-INV-001
- Source raw note: N001, N004, N005
- Expected behavior: Blocked and failed steps persist typed reason codes and recovery options and clear them on valid reactivation.
- Disallowed shallow implementation: Free-text blocked reasons alone cannot drive recovery or reactivation decisions.
- Failing-first test: bundle://proof/SB09/transcripts/failing-first.txt
- Passing test: bundle://proof/SB09/transcripts/passing.txt
- Changed source files: src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs, src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunBlockState.cs, src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs, tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- Production assertions: bundle://proof/SB09/transcripts/source-assertions.txt cites production paths and focused tests.
- Red-team negative case: Free-text blocked reasons alone cannot drive recovery or reactivation decisions.
- Downstream dependency check: reviews/01-execution-report.md gate row for SB09 closes downstream dependency checks.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB09-INV-001 governed behavior | repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunBlockState.cs and dotnet test proof | Closed by bundle://proof/SB09/transcripts/passing.txt | Red-team rejection in bundle://proof/SB09/transcripts/failing-first.txt |
