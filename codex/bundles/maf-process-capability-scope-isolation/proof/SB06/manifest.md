# SB06 Proof Manifest

## Subbundle

- Subbundle: `SB06`
- Status: `Completed`
- Owned requirement: final architecture closure must prove tests, builds, scans, and dependency isolation for the phased MAF/process refactor.
- Test name: `ProjectStructureAgentIntegrationTests`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessCapabilityScopeTranslator.cs` | `B61481DA80712722BB958CC036C9F06E896D6FAE9F561910931FC1A5CC58311E` |

## Proof Artifacts

- Semantic invariant contract: `bundle://proof/SB06/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB06/transcripts/adversarial-negative.txt`
- Passing transcript: `bundle://proof/SB06/transcripts/passing.txt`
- Anti-stub audit transcript: `bundle://proof/SB06/transcripts/anti-stub.txt`
- Architecture gate: `bundle://reviews/csharp-architecture-gate.md`
- Source assertion: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessCapabilityScopeTranslator.cs`

## Closure

- Failing-first: `bundle://proof/SB06/transcripts/adversarial-negative.txt` records the final dependency isolation scan.
- Semantic positive proof: `bundle://proof/SB06/transcripts/passing.txt` records full unit, filtered integration, isolated build, JSON, text-scan, dependency-scan, and CodeAnalytics proof.
- Anti-stub audit: `bundle://proof/SB06/transcripts/anti-stub.txt` records no placeholder implementation in the closure docs and architecture review path.
