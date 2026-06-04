# Requirement Traceability

| Requirement | Input notes | Source areas | Owning subbundle | Proof method |
| --- | --- | --- | --- | --- |
| R01 | N01 | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor`, provider panel | SB01 | Component/browser provider count proof |
| R02 | N02 | `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs` | SB01 | Unit seed/catalog assertion |
| R03 | N03 | Provider models/editors/normalizer/UI | SB01 | Save/reload test and browser TagEditor proof |
| R04 | N03 | Provider tree builder and panel | SB01 | Tree node test and browser proof |
| R05 | N04 | `AgentCapabilitiesPanel` | SB02 | Component render assertion |
| R06 | N06 | `AgentCapabilitiesPanel` filters | SB02 | Component filter tests |
| R07 | N05 | Capability panel CSS/grid | SB02 | Large viewport screenshot review |
| R08 | N09/N11 | Capability details dialog | SB02 | Dialog open-state browser proof and save test |
| R09 | N10 | MCP configuration editor helpers | SB02 | JSON round-trip test |
| R10 | N07 | Capability setup wizard | SB03 | Wizard save tests and browser proof |
| R11 | N08 | Architecture layouts and wizard dialog | SB03 | Imagegen + ASCII layout evidence and screenshot review |
| R12 | N12 | Runtime/chat code | SB03 | Source audit: no chat shortcut/parser changes |
