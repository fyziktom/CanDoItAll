# SB02 Semantic Invariants

## Invariant SB02-INV-001

- Invariant ID: SB02-INV-001
- Source raw note: N004, N005
- Expected behavior: Tool policy enforces typed allowed operations rather than a single product-mutation flag.
- Disallowed shallow implementation: A shallow ProcessAllowsProductMutation-only policy lets validation and runtime-launch tools through the wrong contract.
- Failing-first test: bundle://proof/SB02/transcripts/failing-first.txt
- Passing test: bundle://proof/SB02/transcripts/passing.txt
- Changed source files: src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs, src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs, tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs
- Production assertions: bundle://proof/SB02/transcripts/source-assertions.txt cites production paths and focused tests.
- Red-team negative case: A shallow ProcessAllowsProductMutation-only policy lets validation and runtime-launch tools through the wrong contract.
- Downstream dependency check: reviews/01-execution-report.md gate row for SB02 closes downstream dependency checks.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB02-INV-001 governed behavior | repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs | repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs and dotnet test proof | Closed by bundle://proof/SB02/transcripts/passing.txt | Red-team rejection in bundle://proof/SB02/transcripts/failing-first.txt |
