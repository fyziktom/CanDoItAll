# SB01 - Workflow Boundary Inventory And Project Graph

## Status

- `Completed`

## Objective

Confirm the real workflow, workflow-node, executor, plugin, template, API, UI, persistence, and test surface before any project extraction starts. Produce the dependency-safe project graph and migration rules that every later subbundle must follow.

## Success Criteria

- All discovered workflow and executor source files are assigned to a target owner, a later subbundle, or an explicit exception.
- Existing identifiers that must remain stable are listed: executor ids, template keys, workflow JSON fields, plugin manifest fields, event type names, and process bridge contracts.
- Error-state surfaces are mapped to target owners and proof so exception, plugin, tool/MCP, and runtime failures remain actionable after isolation.
- The target project graph is validated against existing solution organization and the prior tool/skill isolation precedent.
- The XLSX mapping workbook is updated with the final SB01 source map.

## Covered Inputs

- R01, R02, R03, R13, R15, R16, R17, R18.
- Architect note that workflow isolation should follow the successful tool and skill isolation pattern.
- Architect note that all parts must be identified before implementation.

## Prerequisites

- None.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting\AgentFrameworkServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Templates\WorkflowTemplatePack.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Plugins\PluginWorkflowExecutorDescriptorSource.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginExecutionContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginManifestContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryMafIntegration.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\CognitiveMemoryModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\AgentTools\ProjectStructureAgentRuntimeToolProvider.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\SchedulerWorkflowInputSchemaService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\SchedulerWorkflowInputOptionService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\SchedulerPlannerWorkflowInputOptionProviders.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerWorkflowExecutors.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail\GmailWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365\Office365WorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\codex\bundles\skill-tool-mcp-isolation-template-migration`

## Deliverables

- Updated source inventory with exact owner decisions.
- Final project graph and allowed dependency directions.
- Explicit list of compatibility constants and serialized contracts to preserve.
- Error-state inventory ownership for validation, runtime, executor, plugin, external tool/MCP, template, MAF adapter, API/UI, and persistence failures.
- Updated workbook rows for source map, project targets, and risk ownership.

## Dependency Impact

- Every later subbundle depends on this phase. A weak inventory can create circular project references, leave executors in MAF, miss plugin compatibility constraints, or defer hidden work until the final adoption phase. Downstream subbundles must treat SB01 as the authority unless they update the inventory and traceability files first.

## Validation Depth

- `Critical foundation`
- Architecture and source inventory proof.
- No browser validation.

## Implementation Steps

1. Re-run `rg` over `Workflow`, `WorkflowExecutor`, `IWorkflowExecutor`, `PluginWorkflowExecutor`, `Templates/Workflows`, and `WorkflowTemplatePackLoader` to confirm no surfaces were missed.
2. Compare current workflow coupling against the tool/skill isolation bundle and existing process projects.
3. Update `inventories/02-workflow-source-inventory.md`, `inventories/03-executor-inventory.md`, `inventories/05-plugin-consequence-inventory.md`, and `inventories/06-error-state-inventory.md`.
4. Update `architecture/02-project-map-and-adoption-boundary.md` with final project names, dependency rules, and migration owners.
5. Add compatibility constants or identifiers to traceability where later tests must assert parity.
6. Update the XLSX workbook sheets `Source Map`, `Project Targets`, `Executor Categories`, `Plugin Consequences`, `Error States`, and `Validation Matrix`.
7. Run the prepared-stage bundle validator if this subbundle changes bundle structure.

## Scope Exceptions

- Do not move production code in this subbundle.
- Do not settle exact NuGet package versions unless a new project cannot be defined without them.

## Do Not Do

- Do not implement workflow projects.
- Do not edit `.slnx`, `.csproj`, or production source.
- Do not use MAF as the fallback owner for ambiguous workflow behavior; mark ambiguity explicitly.

## Acceptance Checklist

- [x] Every current workflow source file has a target owner and subbundle.
- [x] Every current executor has a category owner and compatibility tests assigned.
- [x] Module-provided executors such as Cognitive Memory have migration owners and tests assigned instead of being hidden under plugin coverage.
- [x] Plugin execution, manifest, grant, package loading, and bundled executors are mapped.
- [x] Workbench agent-tool and scheduler workflow consumers are mapped to adoption/template phases.
- [x] Error-state inventory maps failure diagnostics and negative proof owners.
- [x] Project graph has no allowed dependency from abstractions to MAF, UI, plugins, or persistence implementations.
- [x] Workbook is updated and rendered without visible corruption.

## Proof Required

- `proof/SB01/manifest.md` listing inventory commands, changed bundle files, and workbook preview paths.
- `proof/SB01/semantic-invariants.md` covering stable ids, serialization compatibility, plugin source/trust semantics, and dependency direction invariants.
- Command transcript for the inventory search.
- Workbook render preview for all edited sheets.

## Browser Validation Logging

- `N/A`. This subbundle is architecture and inventory preparation only.

## Progression Gate

- SB02 cannot start until this source inventory and project graph are accepted as current. If implementation discovers a missed workflow or executor surface, return to SB01, update the workbook and traceability, then continue.

## Suggested Agent Prompt

```text
Implement SB01 only. Confirm the workflow/executor/plugin inventory against the repository before changing any production code. Update the bundle inventories, project graph, traceability, and workbook. Do not implement project extraction. Stop if the inventory cannot assign every discovered surface to a target owner or explicit exception.
```
