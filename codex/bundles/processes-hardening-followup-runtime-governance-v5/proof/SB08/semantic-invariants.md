# SB08 Semantic Invariants

## Invariant SB08-INV-001

- Invariant ID: SB08-INV-001
- Source raw note: N001, N004, N005
- Expected behavior: Runtime finalization persists invariant violations and blocks completion on high-severity governance failures.
- Disallowed shallow implementation: Persisted contracts and runtime invariant checks cannot be replaced by prose markers in the prompt.
- Failing-first test: bundle://proof/SB08/transcripts/failing-first.txt
- Passing test: bundle://proof/SB08/transcripts/passing.txt
- Changed source files: src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs, src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs, tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Production assertions: bundle://proof/SB08/transcripts/source-assertions.txt cites production paths and focused tests.
- Red-team negative case: Persisted contracts and runtime invariant checks cannot be replaced by prose markers in the prompt.
- Downstream dependency check: reviews/01-execution-report.md gate row for SB08 closes downstream dependency checks.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB08-INV-001 governed behavior | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs and dotnet test proof | Closed by bundle://proof/SB08/transcripts/passing.txt | Red-team rejection in bundle://proof/SB08/transcripts/failing-first.txt |
