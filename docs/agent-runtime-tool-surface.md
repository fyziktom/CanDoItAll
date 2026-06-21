# Agent Runtime Tool Surface

This page defines the boundary between internal MAF/runtime-provider tools and the HTTP control-plane APIs.

The runtime tool surface is intentionally narrower than the HTTP API surface. Do not tell agents that an operation is a direct tool unless it is registered by MAF itself or by an `IAgentRuntimeToolProvider`, and classified by `AgentToolInvocationPolicy`.

## Process Tools

Source:

- `src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs`
- `src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`
- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `src/CanDoItAll.Web/Api/ProcessesApi.cs`

Current direct runtime tools: 23.

Process tools are owned by `CanDoItAll.Modules.Processes` and exposed through `ProcessAgentRuntimeToolProvider`, which is registered as an `IAgentRuntimeToolProvider` by the Processes module. `CanDoItAll.AgentFramework.Maf` composes registered providers and applies the same approval wrapping rules it applies to its built-in tools. If the Processes module is not registered, MAF starts without process tools instead of carrying a compile-time dependency on `CanDoItAll.Modules.Processes`.

| Capability | Direct tools |
| --- | --- |
| Definition authoring | `processes_definitions_list`, `processes_definition_editor_get`, `processes_definition_save`, `processes_definition_role_add`, `processes_definition_publish`, `processes_definition_delete`, `processes_definition_export`, `processes_definition_import` |
| Run operations | `processes_runs_list`, `processes_run_detail_get`, `processes_analytics_get`, `processes_run_start`, `processes_step_transition`, `processes_assignment_resolve`, `processes_artifact_record` |
| Option catalogs | `processes_party_options_list`, `processes_executor_options_list` |
| Template pack | `processes_templates_list`, `processes_template_get`, `processes_template_mermaid_get`, `processes_template_import`, `processes_template_baseline_scenarios_list`, `processes_template_live_run_profiles_list` |

The following process HTTP operations are HTTP-only until typed runtime tools are deliberately added with policy and approval coverage:

- Launch plans and candidate-selection endpoints.
- Manager directives.
- Direct messages.
- Escalation assign, resolve, reopen, and rework operations.
- Operator approval operations.
- Step-scoped artifact and assignment list/detail endpoints.
- Artifact detail endpoints.
- Template detail, envelope, baseline-scenario, and live-profile HTTP routes beyond the direct template tools listed above.
- Stop/rerun/recovery-style run operations that are exposed only through HTTP.

## Project Structure Tools

Source:

- `src/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`
- `src/CanDoItAll.Modules.Workbench/Services/WorkbenchModuleServiceCollectionExtensions.cs`
- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `src/CanDoItAll.Web/ProjectStructureAgentApi.cs`

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
