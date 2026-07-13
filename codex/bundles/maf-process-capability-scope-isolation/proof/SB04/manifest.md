# SB04 Proof Manifest

## Subbundle

- Subbundle: `SB04`
- Status: `Completed`
- Owned requirement: process capability scope must be translated into MAF runtime metadata without coupling process assemblies to the MAF wrapper project.
- Test name: `Process_execution_metadata_carries_scoped_capability_policy_to_runtime_intent`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessCapabilityScopeTranslator.cs` | `B61481DA80712722BB958CC036C9F06E896D6FAE9F561910931FC1A5CC58311E` |

## Proof Artifacts

- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB04/transcripts/adversarial-negative.txt`
- Passing transcript: `bundle://proof/SB04/transcripts/passing.txt`
- Anti-stub audit transcript: `bundle://proof/SB04/transcripts/anti-stub.txt`
- Source assertion: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessCapabilityScopeTranslator.cs`
- Source assertion: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs`
- Source assertion: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`

## Closure

- Failing-first: `bundle://proof/SB04/transcripts/adversarial-negative.txt` records no process or module reference to the MAF wrapper project.
- Semantic positive proof: `bundle://proof/SB04/transcripts/passing.txt` records runtime intent metadata and scoped prompt tests.
- Anti-stub audit: `bundle://proof/SB04/transcripts/anti-stub.txt` records no placeholder implementation in the handoff path.
