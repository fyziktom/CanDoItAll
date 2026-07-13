# 05-workspace-mcp-context-skill-and-tool-drivers

## Status

- `Ready`

## Objective

Extract the runtime feature drivers currently hidden as nested MAF builders/plugins: workspace tools, storage tools, MCP tools, context providers, skills, built-in tools, and compaction. Each driver must have direct tests and fakeable dependencies.

## Covered Inputs

- M003, M004, M007, M009, M010
- R006, R007, R010, R012

## Prerequisites

- SB02 contracts and registration strategy.
- SB03 capability assembly contract.
- SB04 provider/session contracts where feature drivers need provider data.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceRuntimePlugin.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.StorageRuntimePlugin.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Context.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Skills.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.ConfiguredWorkspace.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentContextContributionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkspaceRuntimeProcessToolsTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkspaceArtifactToolServiceTests.cs`

## Deliverables

- Workspace runtime tool driver/factory with fake workspace file, command, and artifact services.
- MCP capability driver with fakeable MCP client factory and hosted/local/remote branches tested.
- Context capability driver with RAG/static/Mem0 behavior tested without full runtime.
- Skill capability driver with script policy behavior tested directly.
- Built-in tool driver replacing nested switch ownership where appropriate.
- Compaction attachment seam if currently trapped in runtime composition.
- Integration parity through `MafAgentRuntime`.

## Dependency Impact

- SB06 depends on these drivers for fake workspace/MCP/context/skill integration tests.
- SB07 depends on this phase to measure feature-driver setup cost and prove no startup regression.
- Future agent-specific tools depend on these drivers staying isolated from the runtime coordinator.

## Validation Depth

- `Critical feature-driver foundation`

## Implementation Steps

1. Extract workspace runtime tools behind a driver/factory without weakening workspace scope checks.
2. Extract MCP behavior with a fakeable client factory and preserved approval/disposal behavior.
3. Extract context and skill builders into direct collaborators.
4. Extract built-in tool mapping into a tool driver or catalog-backed collaborator.
5. Preserve context manifest sources, diagnostics, progress messages, and effective tool metadata.
6. Add direct tests with fake dependencies and integration parity tests through MAF.
7. Update proof and execution report.

## Scope Exceptions

- Do not redesign workspace, MCP, skill, or context product behavior.
- Do not add new domain-specific tools.
- Do not move Workbench/project-structure domain rules into MAF.

## Do Not Do

- Do not bypass workspace scope or process command policy.
- Do not leak MCP/client resources or skip disposal.
- Do not collapse feature drivers into one new huge class.
- Do not leave old nested builders as active production paths after extraction.

## Acceptance Checklist

- [ ] Feature drivers are directly unit-testable.
- [ ] Fake workspace/MCP/context/skill dependencies can be injected.
- [ ] Existing behavior and policy checks are preserved.
- [ ] Runtime integration parity passes.
- [ ] No domain-specific Financial Strategist work appears.

## Proof Required

- `proof/SB05/manifest.md`
- `proof/SB05/semantic-invariants.md`
- `## Production Behavior Artifact Matrix` for driver outputs, diagnostics, resource leases, and context/tool attachment records.
- Test transcripts for driver tests and integration parity.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Browser Validation Logging

- N/A unless UI-visible diagnostics are added.

## Progression Gate

- SB06/SB07 may rely on this phase only after moved feature behavior can be tested through direct collaborators and still works through `MafAgentRuntime`.

## Suggested Agent Prompt

```text
Implement SB05 only. Extract workspace, MCP, context, skill, storage, built-in tool, and compaction feature drivers from MAF nested builders/plugins. Preserve policies, diagnostics, disposal, and integration parity.
```
