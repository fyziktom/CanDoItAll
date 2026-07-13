# SB02 Proof Manifest

## Scope

- Subbundle: `SB02-agent-runtime-git-tools`
- Status: `Completed`
- Closure date: `2026-06-29`

## Portable References

- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`
- Source assertion map: `bundle://proof/SB02/source-assertions.md`
- Primary command source: `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandPlanBuilder.cs`

## Changed Files

| File | SHA-256 |
| --- | --- |
| `src/CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj` | `823BCF54827017A4627DD60F1A6106F038F31ACAAA610619351D599E5E2E31FA` |
| `src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandPlanBuilder.cs` | `6182FD4695EB919B6A3210CCAFFE721E0D2A231B87986F2A9F61FEE81BCE6CF6` |
| `src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandExecutionService.cs` | `7032BD3ACFD3CE045F779FD54E071AE9190D385BC8D59A60BB22566080ED10C8` |
| `src/CanDoItAll.AgentFramework.Core/Workspace/Process/WorkspaceProcessContracts.cs` | `BF8020A15DBC55E0F4DFB296DCB7E42863C27A4B163C26D80453347550C1CD26` |
| `src/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceRuntimePlugin.cs` | `437A8EAA9A9108CB95BE2D874AAD33228A32CF9720B3AA7DA76F8CBF22825E1E` |
| `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs` | `2FCC447AA9A6A6C4C76131A353F9AB6157C284ACD2CE2A185051912F6BA99F00` |
| `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.ConfiguredWorkspace.cs` | `EFA6048588E5A6A3693367FE9879AF7B21E8736772CB88CF36F37D2BE692D4B0` |
| `src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs` | `BC67CD19C9089779CA019E9D2E01A90EC605C024AFE8CCBAF2AD9FFCEE754A15` |
| `src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs` | `2ED231A6CE3B915F2E88F4885128FB8061134D4CADF229652D435DE97FE1934B` |
| `src/CanDoItAll.AgentFramework.Models/Agents/Access/AgentWorkspaceToolAccessModels.cs` | `67DA4B26A8E2EDBC876DC136F6673E1A07DBB5D15B3D15B128A160AA6FF45717` |
| `tests/CanDoItAll.Tests.Unit/WorkspaceCommandExecutionServiceTests.cs` | `A194AD39355C5C5505D52583FFC23262D64EF5508930E5FF75583EB27BBD1262` |
| `tests/CanDoItAll.Tests.Unit/AgentWorkspaceToolAccessMetadataTests.cs` | `DEF903D762A4890144E47081C61B4FFBA3858EA0B5F6929411DA73EDE2B49844` |
| `tests/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs` | `ADC30AC325454C826F14B97D349D433F91A81B3C412C48FC37A9605D508126E1` |

## Commands

| Command | Transcript | Result |
| --- | --- | --- |
| `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "WorkspaceCommandExecutionServiceTests\|AgentWorkspaceToolAccessMetadataTests\|MafAgentRuntimeToolProviderCompositionTests" --no-restore -p:BuildProjectReferences=false` | `proof/SB02/transcripts/failing-first-runtime-git-tools.txt` | Failed first, missing runtime git methods. |
| `dotnet build src/CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj --no-restore` | `proof/SB02/transcripts/runtime-core-build.txt` | Passed, 0 warnings, 0 errors. |
| `dotnet build src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj --no-restore` | `proof/SB02/transcripts/runtime-maf-build.txt` | Passed, 0 warnings, 0 errors. |
| `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "WorkspaceCommandExecutionServiceTests\|AgentWorkspaceToolAccessMetadataTests\|MafAgentRuntimeToolProviderCompositionTests" --no-restore -p:BuildProjectReferences=false` | `proof/SB02/transcripts/runtime-git-tools-focused-tests.txt` | Passed, 75 tests. |

## Semantic Adequacy

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-runtime-git-tools.txt` records the missing runtime git methods before implementation.
- Semantic positive proof transcript: `bundle://proof/SB02/transcripts/runtime-git-tools-focused-tests.txt` passed the runtime, access, and MAF composition test fixtures.
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt` records no stubs in the changed runtime/tool/policy/test scope.
- Shallow-pass trap: failing-first test proved the methods did not exist before runtime changes; focused tests then proved command shapes, access mapping, and MAF composition.
- Semantic positive proof: command service tests assert exact git argument order for read and mutation tools using the fake process host.
- Adversarial negative proof: command service denies option-like revisions and `.git/config` before process execution; access tests deny git mutations for read-only profiles.
- Anti-stub audit: `proof/SB02/anti-stub-audit.txt` has no TODO, placeholder, stub, or `NotImplementedException` matches in the changed SB02 scope.
- Runtime tool-name proof: `proof/SB02/source-assertions.md` maps each final tool name across command service, MAF composition, policy catalog, registry, access metadata, and tests.

## Closure Decision

SB02 is closed. Agents can receive the bounded local git tool set through configured workspace tools or individual tool capabilities, with git mutations gated as state-changing manage-paths operations.
