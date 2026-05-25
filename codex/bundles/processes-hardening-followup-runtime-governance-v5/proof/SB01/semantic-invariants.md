# SB01 Semantic Invariants

## Invariant SB01-INV-001

- Invariant ID: SB01-INV-001
- Source raw note: N004, N005
- Expected behavior: Persisted step operation contracts survive editor, import/export, publish, and dispatch metadata.
- Disallowed shallow implementation: Text-only contract inference or editor-only state cannot satisfy the invariant.
- Failing-first test: bundle://proof/SB01/transcripts/failing-first.txt
- Passing test: bundle://proof/SB01/transcripts/passing.txt
- Changed source files: src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs, tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- Production assertions: bundle://proof/SB01/transcripts/source-assertions.txt cites production paths and focused tests.
- Red-team negative case: Text-only contract inference or editor-only state cannot satisfy the invariant.
- Downstream dependency check: reviews/01-execution-report.md gate row for SB01 closes downstream dependency checks.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB01-INV-001 governed behavior | repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs | repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessDefinitionEntityConfigurations.cs and dotnet test proof | Closed by bundle://proof/SB01/transcripts/passing.txt | Red-team rejection in bundle://proof/SB01/transcripts/failing-first.txt |
