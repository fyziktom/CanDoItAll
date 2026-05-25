# SB10 Semantic Invariants

## Invariant SB10-INV-001

- Invariant ID: SB10-INV-001
- Source raw note: N002, N003, N004, N005
- Expected behavior: Generic red-team lint and scenario gates reject shallow process definitions without making software-delivery assumptions.
- Disallowed shallow implementation: Architecture/report-only process scenarios must not be forced into product mutation contracts.
- Failing-first test: bundle://proof/SB10/transcripts/failing-first.txt
- Passing test: bundle://proof/SB10/transcripts/passing.txt
- Changed source files: tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs, tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs, tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs
- Production assertions: bundle://proof/SB10/transcripts/source-assertions.txt cites production paths and focused tests.
- Red-team negative case: Architecture/report-only process scenarios must not be forced into product mutation contracts.
- Downstream dependency check: reviews/01-execution-report.md gate row for SB10 closes downstream dependency checks.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB10-INV-001 governed behavior | repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs | repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs and dotnet test proof | Closed by bundle://proof/SB10/transcripts/passing.txt | Red-team rejection in bundle://proof/SB10/transcripts/failing-first.txt |
