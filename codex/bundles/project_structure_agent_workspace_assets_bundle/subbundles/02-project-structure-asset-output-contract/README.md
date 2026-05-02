# Project Structure Asset Output Contract

## Status

- `Completed`

## Objective

Make both internal project-structure tools and ProjectStructure MCP tools state that generated Mermaid diagrams and files must be added as typed file asset nodes.

## Covered Inputs

- `NOTE-02`

## Prerequisites

- Prepared bundle validation passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Tools\MafAgentRuntime.ProjectStructureTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.ProjectStructure\ProjectStructureTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectNodes\ProjectNodeKindRegistry.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureCreateRequestComposer.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.ProjectStructure.Tests\ProjectStructureToolsTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureCanvasCatalogTests.cs`

## Deliverables

- Clear internal tool descriptions for Mermaid and other file node output.
- Clear MCP tool descriptions for Mermaid and other file node output.
- Optional static constants or helpers so internal and MCP wording cannot drift.
- Tests or static assertions that tool descriptions include the canonical Mermaid contract.

## Dependency Impact

- This phase informs agent behavior.
- Validation and closure depend on proving the guidance is visible at every tool surface an agent may use.

## Validation Depth

- Contract-level validation.
- No browser proof required.

## Implementation Steps

1. Add or centralize guidance text for typed file node output.
2. Update internal `project_structure_node_create` and `project_structure_node_update` descriptions.
3. Update MCP `project_structure_node_create` and `project_structure_node_update` descriptions.
4. Confirm the existing model path still detects Mermaid diagram kind for `File` + `mermaid`.
5. Add tests or assertions that descriptions mention `ProjectObjectType.File`, `objectSubtype`, `mermaid`, and `notes`.

## Scope Exceptions

- This phase does not add a new dedicated `project_structure_mermaid_create` tool unless the existing generic create tool cannot be made clear enough.

## Do Not Do

- Do not model Mermaid diagrams as architecture blocks or generic work items.
- Do not remove existing typed block guidance.

## Acceptance Checklist

- Tool descriptions tell agents exactly how to create Mermaid nodes.
- Tool descriptions tell agents to use typed file nodes for other file outputs.
- Existing Mermaid viewer and metadata detection remain compatible.

## Proof Required

- `dotnet test tests/CanDoItAll.Mcp.ProjectStructure.Tests/CanDoItAll.Mcp.ProjectStructure.Tests.csproj --filter Node_create_and_update_descriptions_define_mermaid_file_asset_contract --no-restore -m:1` passed.
- Internal project-structure create/update tool descriptions now tell agents to create Mermaid diagrams as `objectType File`, `objectSubtype mermaid`, with Mermaid source in `notes`.
- MCP create/update descriptions carry the same Mermaid and generated-file asset-node contract.

## Browser Validation Logging

- N/A. This subbundle changes tool contracts, not rendered UI.

## Progression Gate

- Downstream closure may continue only after internal and MCP descriptions expose the typed file node contract.

## Suggested Agent Prompt

```text
Implement subbundle 02 only: update internal and MCP project-structure tool guidance so Mermaid diagrams and other files are created as typed file nodes, then add focused tests that assert the contract is visible.
```
