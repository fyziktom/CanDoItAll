# SB10 - Workflow Template And Descriptor Loading

## Status

- `Completed`

## Objective

Move workflow template pack loading, YAML-to-workflow materialization, preview simulation fixture loading, and descriptor validation out of the Blazor module into workflow-owned template services that consume workflow builders and executor descriptors.

## Success Criteria

- `Templates/Workflows` loading no longer depends on `CanDoItAll.Modules.AgentFramework`.
- Template materialization uses workflow builders/factories instead of duplicated node/edge construction.
- Template executor references validate against executor abstractions and descriptor sources.
- Existing templates, preview simulations, input parameters, routing, and runtime policy mappings load with parity.
- Malformed template failures include template file, template key, workflow key, YAML path, node id, executor id, and repair hint where known.
- Template loader code is split by manifest parsing, DTO validation, workflow materialization, preview simulation loading, descriptor validation, and diagnostics.

## Covered Inputs

- R04, R10, R12, R13, R14, R15, R17, R18.
- Architect note that workflow builders/factories should support workflow creation.

## Prerequisites

- SB09 passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Templates\WorkflowTemplatePack.cs`
- `C:\repositories\CanDoItAll\Templates\Workflows\manifest.yaml`
- `C:\repositories\CanDoItAll\Templates\Workflows\workflows`
- `C:\repositories\CanDoItAll\Templates\Workflows\preview-simulations\executors.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Core\WorkflowCatalogServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Core\BuiltInWorkflowExecutorDescriptors.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\SchedulerWorkflowInputSchemaService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\SchedulerWorkflowInputOptionService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\SchedulerPlannerWorkflowInputOptionProviders.cs`

## Deliverables

- `CanDoItAll.AgentFramework.Workflows.Templates` project for manifest loading, YAML DTO parsing, workflow materialization, input parameter mapping, preview simulation loading, descriptor validation, and template diagnostics.
- Focused helper/service split from `WorkflowTemplatePackLoader` so the new template project does not become a copied Blazor-module monolith.
- Tests for every current template manifest/workflow file.
- Tests for missing executor, invalid routing, invalid input parameter, invalid runtime policy, invalid YAML/JSON, invalid settings, and invalid preview simulation cases with typed diagnostics.
- Updated Blazor module code that consumes the template service through abstractions but does not own template loading logic.
- Scheduler workflow input option consumers updated to use template/input contracts without depending on UI-owned template DTOs.

## Dependency Impact

- SB11 MAF adapter and SB12 UI/API adoption depend on template services loading workflows through isolated projects. SB14 final closure must prove template compatibility. If template code stays in the UI module, the architecture remains inverted.

## Validation Depth

- `Critical template foundation`
- Unit, template fixture integration, descriptor validation, and diagnostics proof.

## Implementation Steps

1. Move template loading logic and DTOs into the workflow template project.
2. Split manifest parsing, DTO validation, workflow materialization, preview simulation loading, descriptor validation, and diagnostic mapping into focused helpers/services.
3. Replace duplicated graph construction with SB02 builders/factories.
4. Validate template executor ids against executor descriptor sources from SB06-SB09.
5. Preserve template keys, YAML field names, runtime policy mappings, input parameter shapes, and preview simulation semantics.
6. Add tests that load all existing templates from `Templates/Workflows`.
7. Add negative tests for broken manifests, missing executors, invalid ports, invalid settings, invalid YAML/JSON, and malformed preview simulations.
8. Update UI/module references only enough to consume the new template service.

## Scope Exceptions

- MAF compiler/backend adapter isolation is SB11.
- Workflow page and editor UX adoption is SB12.

## Do Not Do

- Do not create a second template format.
- Do not silently skip invalid workflow templates.
- Do not leave template materialization in the Blazor module as a fallback.
- Do not move `WorkflowTemplatePackLoader` as one large class without splitting responsibilities and tests.
- Do not report template failures without file/key/node/executor context when the context is known.

## Acceptance Checklist

- [x] Template project compiles without UI or MAF implementation dependencies.
- [x] All existing templates load and materialize with stable keys.
- [x] Descriptor validation catches missing or incompatible executor references.
- [x] Preview simulation fixtures load with parity.
- [x] Malformed-template diagnostics include actionable context and repair hints.
- [x] Template services pass file-size/responsibility review.
- [x] Blazor module delegates template loading to the workflow template service.

## Execution Notes

- Added `CanDoItAll.AgentFramework.Workflows.Templates` for manifest parsing, YAML DTOs, input parameter materialization, workflow graph materialization, preview fixture loading, descriptor validation, and typed template diagnostics.
- Deleted the UI-owned `Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs`; module startup now composes template services through `AddWorkflowTemplateServices()`.
- Template materialization uses `WorkflowDefinitionBuilder`, `WorkflowNodeBuilder`, `WorkflowEdgeBuilder`, and `WorkflowPortBuilder`.
- Focused tests load every current template and cover missing executor, invalid routing, invalid input parameter, invalid runtime policy, invalid executor settings, invalid YAML, malformed preview simulation JSON, descriptor validation, and no UI fallback.
- Visible workflow template selection behavior was not intentionally changed; browser proof remains deferred to SB12/SB13/SB14, with small and medium viewport tests skipped per user instruction.

## Proof Required

- `proof/SB10/manifest.md` with file hashes, all-template load transcripts, negative test transcripts, and descriptor validation proof.
- `proof/SB10/semantic-invariants.md` covering template key stability, YAML compatibility, builder-based materialization, typed explicit invalid-template failures, repair hints, preview simulation parity, file responsibility, and no UI fallback.
- Semantic Adequacy Gate proof with adversarial malformed template cases, positive current template load cases, and anti-stub audit.

## Browser Validation Logging

- `N/A` unless the implementation changes visible workflow template selection. If visible behavior changes, defer browser proof to SB12 and record the deferral.

## Progression Gate

- SB11 cannot start until templates load through the isolated template service and descriptor validation proves both default and plugin executor references are compatible.

## Suggested Agent Prompt

```text
Implement SB10 only. Move workflow template loading and descriptor validation out of the Blazor module into workflow-owned template services. Split the loader by responsibility, use the workflow builders and executor descriptors from earlier phases, preserve template compatibility, add all-template and diagnostic negative tests, and capture Semantic Adequacy Gate proof. Do not isolate MAF backend or perform broad UI adoption.
```
