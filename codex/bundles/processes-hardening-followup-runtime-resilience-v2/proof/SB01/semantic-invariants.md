# SB01 Semantic Invariants

## SB01-INV-001

- Invariant ID: `SB01-INV-001`
- Source raw note: N001, N003, N007
- Expected behavior: process dispatch carries an explicit generic operation contract that distinguishes managed process artifacts, external artifact destinations, and product target mutation.
- Disallowed shallow implementation: prompt-only wording, software-only keyword lists, source assertions without runtime metadata, or tests that only inspect static tables.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB01/transcripts/passing.txt`
- Changed source files and hashes: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Red-team negative case: business/research artifact creation does not produce a product-mutation warning.
- Downstream dependency check: SB02 consumes the product-mutation metadata for tool policy enforcement.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessStepOperationContract` metadata | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs` | `repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs` | `bundle://proof/SB01/transcripts/source-assertions.txt` | `bundle://proof/SB01/transcripts/failing-first.txt` |
