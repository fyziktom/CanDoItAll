# mcp-and-internal-agent-action-contracts

## Status

- `Completed`

## Objective

Expose project-structure node action capability information through the Project Structure MCP and internal agent project-structure tools so agents can understand runtime, local file, and IPFS node behavior without parsing raw metadata.

## Covered Inputs

- `N005`
- `REQ-TOOLS-001`
- `REQ-TOOLS-002`
- `REQ-TOOLS-003`

## Prerequisites

- Subbundle 01 completed or honestly blocked.
- Subbundle 02 completed or honestly blocked.
- Final runtime/local/IPFS capability semantics are known.

## Exact Source References

- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentContracts.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentService.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Mcp.ProjectStructure/ProjectStructureTools.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProjectStructureTools.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Mcp.ProjectStructure.Tests/ProjectStructureToolsTests.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Integration/ProjectStructureAgentApiIntegrationTests.cs`

## Deliverables

- Node summaries include structured action-capability data for runtime normal/admin, File Explorer, and new-tab/IPFS behavior.
- Project Structure MCP `project_structure_read` returns the capability data.
- Internal agent project-structure compact nodes include the same data.
- Tool descriptions document that these are host/UI capabilities, not remote execution APIs.
- Tests cover the new contract fields.

## Dependency Impact

- This is the final contract propagation phase. Final bundle closure depends on this subbundle.

## Validation Depth

- Contract-critical closure.

## Implementation Steps

1. Add a compact action capability record to `ProjectStructureAgentContracts.cs`.
2. Populate it in `ProjectStructureAgentService.MapNodeSummary` using the same Workbench capability logic as the UI.
3. Extend MCP tool descriptions to mention runtime/file/IPFS capability metadata.
4. Extend `MafAgentRuntime.ProjectStructureTools` compact node mapping.
5. Add tests for Project Structure MCP and internal agent compact mapping as locally practical.
6. Update execution-report raw-note closure for `N005`.

## Scope Exceptions

- Agents receive capability metadata only. They do not receive a new host launcher/open-file tool in this bundle.

## Do Not Do

- Do not add local host execution through MCP.
- Do not force `IncludeMetadata` just to expose capabilities.
- Do not expose raw secrets or local absolute paths beyond safe display fields already allowed by the app.

## Acceptance Checklist

- `project_structure_read` node payloads include action capability metadata.
- Internal agent compact nodes include the same capability metadata.
- Tool descriptions explain runtime normal/admin and file/IPFS action semantics.
- Tests prove the fields are populated for representative runtime/local/IPFS nodes.

## Completion Evidence

- `ProjectStructureNodeActionCapabilities` and action descriptors are part of the shared Workbench agent contract.
- `ProjectStructureAgentService` populates node capability metadata using runtime launcher and local file opener services.
- Project Structure MCP compact nodes map `ActionCapabilities` and `project_structure_read` documents the action IDs.
- MAF internal project-structure tools map the same compact action capability data and document the same semantics.
- MCP tests cover capability propagation through coordinator and tool responses; MAF project builds with the new contract.

## Proof Required

- Targeted MCP tests.
- Targeted internal agent/tool mapping tests or compile coverage if direct tests are not available.
- Contract inspection showing no remote host execution tool was added.

## Browser Validation Logging

- `N/A` for browser-visible proof unless a final UI smoke is needed because prior subbundles changed capability rendering.

## Progression Gate

- Bundle closure may proceed only after MCP and internal agent tools expose the agreed capability metadata or a concrete blocker/follow-up subbundle is created.

## Suggested Agent Prompt

```text
Implement only subbundle 03. Propagate the final node action capability model into Project Structure MCP and internal agent project-structure tools. Do not add remote host-launch tools. Update tests and execution-report proof.
```
