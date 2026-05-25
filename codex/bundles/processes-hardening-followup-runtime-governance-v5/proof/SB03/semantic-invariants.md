# SB03 Semantic Invariants

## Invariant SB03-INV-001

- Invariant ID: SB03-INV-001
- Source raw note: N004, N005
- Expected behavior: External target aliases are grounded through typed trusted sources with intended use and trust level.
- Disallowed shallow implementation: Free-text prompt aliases cannot become writable process targets without trusted current-run grounding.
- Failing-first test: bundle://proof/SB03/transcripts/failing-first.txt
- Passing test: bundle://proof/SB03/transcripts/passing.txt
- Changed source files: src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs, src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs, tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Production assertions: bundle://proof/SB03/transcripts/source-assertions.txt cites production paths and focused tests.
- Red-team negative case: Free-text prompt aliases cannot become writable process targets without trusted current-run grounding.
- Downstream dependency check: reviews/01-execution-report.md gate row for SB03 closes downstream dependency checks.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB03-INV-001 governed behavior | repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs and dotnet test proof | Closed by bundle://proof/SB03/transcripts/passing.txt | Red-team rejection in bundle://proof/SB03/transcripts/failing-first.txt |
