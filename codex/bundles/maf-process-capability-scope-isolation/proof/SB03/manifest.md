# SB03 Proof Manifest

## Subbundle

- Subbundle: `SB03`
- Status: `Completed`
- Owned requirement: process templates and assignments must carry a typed capability scope and scoped instruction contract through launch and persistence.
- Test name: `Process_template_json_deserializes_capability_scope_contract`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs` | `87CAA49FD36664194A9BF85E63A52284A5FE04C31307F24AB9F5E4A910BBBBA9` |

## Proof Artifacts

- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB03/transcripts/adversarial-negative.txt`
- Passing transcript: `bundle://proof/SB03/transcripts/passing.txt`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub.txt`
- Source assertion: `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs`
- Source assertion: `repo://src/Processes/CanDoItAll.Processes.Persistence/EfProcessRuntimeStepAssignmentStore.cs`
- Source assertion: `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260707134848_ProcessRuntimeAssignmentCapabilityScope.cs`

## Closure

- Failing-first: `bundle://proof/SB03/transcripts/adversarial-negative.txt` records no null assignment of persisted capability scope fields.
- Semantic positive proof: `bundle://proof/SB03/transcripts/passing.txt` records template and persistence round-trip tests.
- Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub.txt` records no placeholder implementation in the process contract and persistence path.
