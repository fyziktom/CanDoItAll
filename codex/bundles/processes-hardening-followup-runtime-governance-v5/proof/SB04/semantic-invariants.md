# SB04 Semantic Invariants

## Invariant SB04-INV-001

- Invariant ID: SB04-INV-001
- Source raw note: N001, N005
- Expected behavior: Artifact validation reads storage references through storage catalog and drivers before validating content.
- Disallowed shallow implementation: Serialized storage-reference JSON cannot be treated as a raw workspace path or skipped before format checks.
- Failing-first test: bundle://proof/SB04/transcripts/failing-first.txt
- Passing test: bundle://proof/SB04/transcripts/passing.txt
- Changed source files: src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs, src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs, tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Production assertions: bundle://proof/SB04/transcripts/source-assertions.txt cites production paths and focused tests.
- Red-team negative case: Serialized storage-reference JSON cannot be treated as a raw workspace path or skipped before format checks.
- Downstream dependency check: reviews/01-execution-report.md gate row for SB04 closes downstream dependency checks.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB04-INV-001 governed behavior | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs and dotnet test proof | Closed by bundle://proof/SB04/transcripts/passing.txt | Red-team rejection in bundle://proof/SB04/transcripts/failing-first.txt |
