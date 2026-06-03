# Process Tool Parity Inventory

Every tool in this table must still be available after migration. Tests must enumerate names explicitly.

| Tool name | Kind | Current purpose | Required post-migration behavior |
| --- | --- | --- | --- |
| `processes_definitions_list` | Read | List process definitions; project scoped optional | Must remain approval-free |
| `processes_definition_editor_get` | Read | Load full process definition editor model / blank editor | Must remain approval-free |
| `processes_definition_save` | Mutation | Create/update process definition editor model | Must require approval unless suppressApprovalRequirements is true |
| `processes_definition_role_add` | Mutation | Add one role requirement without rewriting full editor | Must require approval unless suppressApprovalRequirements is true |
| `processes_definition_publish` | Mutation | Publish current draft process definition | Must require approval unless suppressApprovalRequirements is true |
| `processes_definition_delete` | Mutation | Delete process definition and related runtime records | Must require approval unless suppressApprovalRequirements is true |
| `processes_definition_export` | Read | Export definition envelope | Must remain approval-free |
| `processes_definition_import` | Mutation | Import process definition envelope | Must require approval unless suppressApprovalRequirements is true |
| `processes_runs_list` | Read | List process runs | Must remain approval-free |
| `processes_run_detail_get` | Read | Load process run health, step runs, decisions, artifacts, assignments, work briefs, conformance observations, improvements | Must remain approval-free |
| `processes_analytics_get` | Read | Return process analytics summary | Must remain approval-free |
| `processes_run_start` | Mutation | Start a process run | Must require approval unless suppressApprovalRequirements is true |
| `processes_step_transition` | Mutation | Transition a process step run | Must require approval unless suppressApprovalRequirements is true |
| `processes_assignment_resolve` | Mutation | Resolve/update process assignment | Must require approval unless suppressApprovalRequirements is true |
| `processes_artifact_record` | Mutation | Record process artifact metadata | Must require approval unless suppressApprovalRequirements is true and artifact policy allows |
| `processes_party_options_list` | Read | List project party assignment options | Must remain approval-free |
| `processes_executor_options_list` | Read | List executor registry options | Must remain approval-free |
| `processes_templates_list` | Read | List folder-based process template pack entries | Must remain approval-free |
| `processes_template_get` | Read | Load detailed process template, compatibility report, supporting files | Must remain approval-free |
| `processes_template_mermaid_get` | Read | Export template Mermaid/supporting files | Must remain approval-free |
| `processes_template_import` | Mutation | Import projected template and optionally publish | Must require approval unless suppressApprovalRequirements is true |
| `processes_template_baseline_scenarios_list` | Read | List baseline runtime scenarios from template pack | Must remain approval-free |
| `processes_template_live_run_profiles_list` | Read | List fresh live-run profiles and fresh-run policy | Must remain approval-free |

## Tool Count

Expected process tool count: `23`.

Do not use only this count as proof. Count is a secondary assertion; exact-name parity is mandatory.

## Policy Sources

- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`
- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs`

## Migration Rule

All process tool creation logic must leave MAF and land in the Processes module, but these policy source files remain the canonical policy registry until a later bundle deliberately changes them.
