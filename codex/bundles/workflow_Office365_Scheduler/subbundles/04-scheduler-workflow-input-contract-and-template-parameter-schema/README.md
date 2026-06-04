# 04-scheduler-workflow-input-contract-and-template-parameter-schema

## Status

- Status: `Completed`

## Objective

Introduce a reusable workflow input parameter schema so Scheduler can render a typed form instead of raw JSON for common workflow templates.

## Covered Inputs

- R8: Scheduler can select a workflow and configure typed input fields for email/contact, project, parent node, processed category, and interval.
- R12: file-backed templates expose durable metadata that the loader and saved workflow definitions preserve.

## Prerequisites

- SB03 templates exist and include planned parameter metadata.
- Existing workflow definition/template persistence model is reviewed before adding metadata fields.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowCatalogModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowCatalogContracts.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerModels.cs`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowTemplatePackLoaderTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/SchedulerPlannerIntegrationTests.cs`

## Scope

- Add strongly typed workflow input parameter descriptors and option models.
- Parse `inputParameters` from template metadata.
- Preserve descriptors when workflow definitions are saved from templates.
- Add Scheduler schema resolution service and required-parameter validation.
- Preserve raw JSON fallback for workflows without descriptors.

## Dependency Impact

- SB05 Scheduler UI uses this schema to render typed fields and request option provider values.
- SB06/SB07 rely on validation and normalized input to classify no-message and retry behavior.

## Validation Depth

- Critical semantic proof for template metadata round-trip, saved workflow preservation, schema resolution, invalid required value rejection, and raw JSON fallback.
- Unit and integration tests for loader, persistence, and Scheduler schema service.
- Source assertions proving descriptors are strongly typed rather than parsed from description strings.

## Implementation Steps

1. Add descriptor, kind, option, schema, and validation models.
2. Extend template loader metadata parsing.
3. Preserve schema metadata during workflow save/seed paths.
4. Add `ISchedulerWorkflowInputSchemaService`.
5. Add Scheduler validation for required descriptor values.
6. Add tests and proof artifacts.

## Do Not Do

- Do not overload free-form description text with parameter metadata.
- Do not replace raw JSON fallback.
- Do not introduce a broad module dependency from Scheduler to CRM/Workbench/Office365 for option lookup in this phase.

## Acceptance Checklist

- `inputParameters` YAML maps into typed descriptors.
- Saved workflow definitions preserve descriptors.
- Scheduler resolves schema for a selected workflow.
- Invalid required parameters prevent schedule save.
- Workflows without descriptors still use raw JSON.

## Proof Required

- `bundle://proof/SB04/manifest.md`
- `bundle://proof/SB04/semantic-invariants.md`
- Failing-first transcript for missing schema/validation.
- Passing loader/schema/Scheduler tests.
- Source assertion and anti-stub audit transcripts.

## Browser Validation Logging

- N/A unless this subbundle changes visible Scheduler rendering. SB05 owns browser proof for the typed form.

## Progression Gate

- Passed to SB05. Scheduler can resolve and validate typed workflow input schemas from saved workflow definitions without depending on UI-only logic.

## Closure Notes

- Added strongly typed workflow input descriptor and option-source models.
- Parsed `inputParameters` from file-backed workflow templates and preserved descriptors during workflow seed/save/status/import paths.
- Added `ISchedulerWorkflowInputSchemaService` and enforced required descriptor validation in `SchedulerPlannerService.SavePlanAsync`.
- Preserved raw JSON fallback for workflows without descriptors.
- Proof is recorded in `bundle://proof/SB04/manifest.md` and `bundle://proof/SB04/semantic-invariants.md`.

## Suggested Agent Prompt

Implement the workflow input parameter schema contract and Scheduler schema resolver with tests, keeping raw JSON fallback and deferring option-provider UI work to SB05.
