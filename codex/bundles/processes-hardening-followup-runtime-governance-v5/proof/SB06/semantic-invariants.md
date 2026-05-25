# SB06 Semantic Invariants

## Invariant SB06-INV-001

- Invariant ID: SB06-INV-001
- Source raw note: N002, N003, N005
- Expected behavior: Workflow and subprocess outputs map explicitly to process artifact expectations without same-kind guesses.
- Disallowed shallow implementation: Same-kind heuristic mapping is rejected when multiple workflow or subprocess outputs conflict.
- Failing-first test: bundle://proof/SB06/transcripts/failing-first.txt
- Passing test: bundle://proof/SB06/transcripts/passing.txt
- Changed source files: src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs, tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Production assertions: bundle://proof/SB06/transcripts/source-assertions.txt cites production paths and focused tests.
- Red-team negative case: Same-kind heuristic mapping is rejected when multiple workflow or subprocess outputs conflict.
- Downstream dependency check: reviews/01-execution-report.md gate row for SB06 closes downstream dependency checks.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB06-INV-001 governed behavior | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs | repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs and dotnet test proof | Closed by bundle://proof/SB06/transcripts/passing.txt | Red-team rejection in bundle://proof/SB06/transcripts/failing-first.txt |
