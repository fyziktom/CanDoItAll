# SB07 Semantic Invariants

## Invariant SB07-INV-001

- Invariant ID: SB07-INV-001
- Source raw note: N001, N003, N005
- Expected behavior: Recovery continuation handles missing own outputs and manager-recovery artifacts without workflow/process confusion.
- Disallowed shallow implementation: Negative branch disposition cannot hide a missing required own artifact.
- Failing-first test: bundle://proof/SB07/transcripts/failing-first.txt
- Passing test: bundle://proof/SB07/transcripts/passing.txt
- Changed source files: src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs, src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs, tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Production assertions: bundle://proof/SB07/transcripts/source-assertions.txt cites production paths and focused tests.
- Red-team negative case: Negative branch disposition cannot hide a missing required own artifact.
- Downstream dependency check: reviews/01-execution-report.md gate row for SB07 closes downstream dependency checks.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB07-INV-001 governed behavior | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs and dotnet test proof | Closed by bundle://proof/SB07/transcripts/passing.txt | Red-team rejection in bundle://proof/SB07/transcripts/failing-first.txt |
