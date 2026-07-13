# SB03 Proof Manifest

## Subbundle

- Subbundle: `03-hr-readiness-capability-guardrails`
- Status: `Completed`
- Owned requirement: make launch/readiness and runtime adapter paths catch semantic operation/tool gaps before false escalation.

## Changed Files And Hashes

| File | SHA-256 |
| --- | --- |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs` | `30B6C7C742D51DB17CC71F8C85FB9E45791491AE7B54721D1B9FC42947BAA677` |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | `C3FA37C7E9E3031B547704AD8492E7264481B8450ECB060FA5D3A741433BEA23` |

## Proof Artifacts

- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB03/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB03/transcripts/passing.txt`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub.txt`
- Source assertion: `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`

## Closure

- Failing-first: `bundle://proof/SB03/transcripts/failing-first.txt` records the missing semantic readiness and managed-artifact retry failures.
- Semantic positive proof: `bundle://proof/SB03/transcripts/passing.txt` records focused resolver, adapter, and finalizer tests passing.
- Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub.txt`.
