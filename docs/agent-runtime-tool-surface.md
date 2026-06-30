# Agent Runtime Tool Surface

Last source review: 2026-06-29.

This page defines the boundary between internal MAF/runtime-provider tools and the HTTP control-plane APIs.

The runtime tool surface is intentionally narrower than the HTTP API surface. Do not tell agents that an operation is a direct tool unless it is registered by MAF itself or by an `IAgentRuntimeToolProvider`, and classified by `AgentToolInvocationPolicy`.

Capability templates in `Templates/Capabilities` seed catalog metadata and access policy inputs. They do not by themselves create executable direct tools; the runtime still needs a MAF built-in tool, a provider-native tool, a local/remote MCP descriptor, or an `IAgentRuntimeToolProvider` that returns a typed `AITool`.

## Process Tools

Source:

- `src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `src/App/CanDoItAll.Web/Api/ProcessesApi.cs`

Current direct process runtime tools: 0.

The current source tree does not contain `src/Modules/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs`, and `AddProcessesModule` does not register an `IAgentRuntimeToolProvider` for direct `processes_*` tools. Policy constants and some tests still mention legacy or planned `processes_*` names; treat that as a hardening gap, not as proof that those tools are currently available.

Current process control paths are:

- HTTP routes in `src/App/CanDoItAll.Web/Api/ProcessesApi.cs`: contract discovery, launch preflight, launch, dispatch, cancel, step rework, live, detail, and history.
- Governed process execution through `AgentFrameworkProcessExecutionAdapter`, which attaches workspace, skill, MCP, provider-native, and registered runtime-provider tools according to the process step operation contract.
- Project-structure runtime tools in Workbench that can link or start process definitions from project nodes.
- Blazor process workspace UI backed by process projection services.

If direct process tools are reintroduced, they must be implemented as a concrete `IAgentRuntimeToolProvider` owned by `CanDoItAll.Modules.Processes`, with typed models, explicit process access metadata, `AgentToolInvocationPolicy` classification, approval behavior, and tests. Until then, document process operations as HTTP API or project-structure bridge operations only.

## Project Structure Tools

Source:

- `src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`
- `src/Modules/CanDoItAll.Modules.Workbench/Services/WorkbenchModuleServiceCollectionExtensions.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `src/App/CanDoItAll.Web/ProjectStructureAgentApi.cs`

Current direct runtime tools: 51.

| Capability | Direct tools |
| --- | --- |
| Project hierarchy | `project_structure_projects_list`, `project_structure_project_create`, `project_structure_project_update`, `project_structure_hierarchy_get`, `project_structure_subproject_link`, `project_structure_nodes_to_new_subproject` |
| Structure read/write | `project_structure_read`, `project_structure_node_catalog`, `project_structure_node_create`, `project_structure_node_update`, `project_structure_node_type_update`, `project_structure_node_metadata_update`, `project_structure_nodes_status_update`, `project_structure_node_status_update`, `project_structure_nodes_progress_update`, `project_structure_node_progress_update`, `project_structure_nodes_marker_update`, `project_structure_node_marker_update`, `project_structure_nodes_priority_update`, `project_structure_node_priority_update`, `project_structure_node_move`, `project_structure_node_recompose`, `project_structure_node_reparent`, `project_structure_node_descendants_to_project_move`, `project_structure_node_delete` |
| Node operations | `project_structure_node_command_execute`, `project_structure_node_process_definition_link`, `project_structure_node_process_start`, `project_structure_node_workflow_add_options`, `project_structure_node_workflow_definition_create`, `project_structure_node_workflow_start`, `project_structure_node_workflow_status_get` |
| Planning and links | `project_structure_checklist`, `project_structure_dependencies_query`, `project_structure_dependency_link`, `project_structure_dependency_unlink`, `project_structure_link_create`, `project_structure_link_unlink`, `project_structure_approval_request`, `project_structure_knowledge_query`, `project_structure_analytics_query` |
| Assets and imports | `project_structure_asset_create`, `project_structure_asset_get`, `project_structure_asset_content_get`, `project_structure_asset_create_revision`, `project_structure_import` |
| Leases | `project_structure_project_lease_acquire`, `project_structure_repo_branch_lease_acquire`, `project_structure_lease_get`, `project_structure_lease_renew`, `project_structure_lease_release` |

The current project-structure HTTP route set is covered by typed runtime tools. New project-structure HTTP operations must remain HTTP-only until they have explicit tool registration, policy classification, approval behavior, and tests.

## Adding A Direct Tool

Adding a direct tool is a runtime/security change, not documentation cleanup. The minimum implementation set is:

- Tool descriptor registration in the relevant MAF tool builder or owning `IAgentRuntimeToolProvider`.
- Strongly typed request/response shape or reuse of an existing strongly typed model.
- Service-layer call through the owning module boundary.
- `AgentToolInvocationPolicy` constant and classification.
- Approval requirement review for mutation, destructive, launch, process, workflow, filesystem, or external side-effect operations.
- Unit or integration coverage that proves descriptor availability, policy behavior, and the intended service call.

If that set is not implemented, document the operation as HTTP-only and direct agents to the relevant API skill.
