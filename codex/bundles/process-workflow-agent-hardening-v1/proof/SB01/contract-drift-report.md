# SB01 Contract Drift Report

## Scope

SB01 scanned the contract surfaces named by the subbundle:

- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService*.cs`
- `src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`
- `Templates/Processes/processes/software-delivery/definition.json`
- `Templates/Processes/seed-catalog/baseline-scenarios.json`
- `codex/skills/candoitall-api-processes/SKILL.md`
- `codex/skills/candoitall-api-workflows/SKILL.md`

## Classification Table

| Classification | Ownership rule | Examples | Enforcement |
| --- | --- | --- | --- |
| Internal canonical | Runtime-owned identifiers that production code dispatches, authorizes, validates, or interprets. | Process operations, target scopes, workspace/browser/process/project-structure tool ids, workflow JSON paths. | `ProcessOperationContractNames`, `WorkflowJsonPathContractNames`, `ToolContractCatalog`, `ProcessContractCatalog`, scanner tests. |
| External boundary | Provider/plugin or skill/API protocol identifiers that are consumed across a boundary and cannot be freely renamed. | `office365.messages-by-category`, `gmail.messages-by-label`, skill markdown payload examples. | Explicit allowlist in scanner tests; later subbundles may move provider ids into plugin-owned catalogs. |
| Template content | Process template JSON and seed catalog examples. | `AllowedOperations`, `OperationTargetScope`, template JSON paths. | Structured template validation plus scanner classification. |
| UI label | Human-facing text that is not used as an id or dispatch key. | Component labels, explanatory text. | Out of scanner scope unless it matches an internal id pattern. |
| Test fixture | Deliberate test-only literals and adversarial scanner inputs. | `workspace_destroy_everything`, `workspace_fixture_only`, `missing.executor`. | Test paths are ignored by repository scanner; adversarial test asserts rejection explicitly. |

## Baseline Findings

The initial scoped scan found recurring internal workflow JSON path selectors in `WorkflowCanvasEditor.razor.cs` that were not cataloged:

- `$.projectId`
- `$.project.id`
- `$.nodeId`
- `$.runContext.workflowNodeId`
- `$.category`
- `$.targets`

Resolution: added those selectors to `WorkflowJsonPathContractNames` instead of exempting the file.

No unowned internal tool ids or process operation ids remain in the scoped baseline scan. The deliberate negative test proves `workspace_destroy_everything` is rejected when it appears in a production source path.

## Descriptor Inventory

| Contract family | Canonical owner | Notes |
| --- | --- | --- |
| Process operations and target scopes | `src/CanDoItAll.AgentFramework.Models/Contracts/ProcessOperationContractNames.cs` | Mirrors `ProcessStepOperation` and `ProcessStepTargetScope`; parity test compares against runtime enums. |
| Workflow JSON paths | `src/CanDoItAll.AgentFramework.Models/Contracts/ProcessOperationContractNames.cs` | Covers route, status, project/node selectors, evidence refs, tool names, tasks, and plugin message id selectors. |
| Provider usage phases | `src/CanDoItAll.AgentFramework.Models/Contracts/ProcessOperationContractNames.cs` | Establishes canonical phase names for SB03. |
| Workspace/browser tool ids | `src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs` | Used by tool policy and process dispatch hot-path tool lists. |
| Process operation descriptors | `src/CanDoItAll.Modules.Processes/Definitions/ProcessContractCatalog.cs` | Adds runtime enum descriptors with implied target scope and mutation/external-action traits. |

## Production Behavior Artifact Matrix

| Contract artifact | Producer | Consumer | Lifecycle | Negative tests |
| --- | --- | --- | --- | --- |
| `ProcessOperationContractNames` | Agent framework model contracts | Tool policy, process catalog, template scanner | Static contract names must match runtime enums before dispatch refactors use them. | `Process_operation_contract_names_match_runtime_enums` fails on enum/name drift. |
| `WorkflowJsonPathContractNames` | Agent framework model contracts | Workflow editor, process templates, drift scanner | Static selector inventory grows only when internal selectors are intentionally owned. | `Scoped_repository_contract_drift_scan_has_no_unowned_internal_ids` fails on uncataloged internal JSON paths. |
| `ToolContractCatalog` | Agent framework core | Tool policy and process dispatch | Static tool id inventory centralizes workspace/browser ids and representative tool groups. | `Scanner_rejects_unowned_internal_tool_id` rejects unknown internal ids; `Tool_contract_catalog_contains_process_runtime_tool_surfaces` verifies required surfaces. |
| `ProcessContractCatalog` | Processes module | Process template validation and downstream dispatch refactors | Runtime enum descriptors connect operation ids to scope and mutation/external-action semantics. | `Process_template_operation_ids_are_known` rejects template operations not present in descriptors. |

## Downstream Reopen Triggers

Reopen SB01 before proceeding if a later subbundle introduces:

- A new process operation, target scope, artifact status, workflow selector, tool id, executor id, or usage phase without a canonical owner.
- A template/skill/API literal that is promoted from example content into runtime dispatch logic.
- A scanner exception that is not traceable to external boundary, template content, UI label, or test fixture classification.
- A public id rename without compatibility handling.
