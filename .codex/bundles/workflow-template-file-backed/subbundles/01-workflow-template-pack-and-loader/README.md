# workflow-template-pack-and-loader

## Status

- `Completed`

## Objective

- Add the file-backed YAML workflow-template pack and a typed loader that converts templates into the existing CanDoItAll workflow model.

## Covered Inputs

- R1 default workflows must be text files.
- R2 templates must use YAML and file-backed loading.
- R3 loaded templates must become strongly typed `WorkflowDefinition` graphs before validation.
- R5 YAML load failures must be explicit.
- R6 folder and manifest layout must support future catalogue/sharing work.

## Prerequisites

- Bundle prepared-stage validator passes.
- Current compiled seed source and existing process-template loader precedent have been reviewed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowExampleCatalogSeedService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Templates\ProcessTemplatePackLoader.cs
- C:\repositories\CanDoItAll\Templates\Processes\manifest.json
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowDefinitionValidator.cs
- C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows.Declarative\DeclarativeWorkflowBuilder.cs
- C:\repositories\agent-framework\dotnet\samples\03-workflows\Declarative\ConfirmInput\ConfirmInput.yaml

## Deliverables

- `Templates\Workflows\manifest.yaml`.
- Per-workflow YAML definition files under `Templates\Workflows\workflows`.
- Typed loader and DTO classes that fail with file/key context on malformed templates.
- Unit coverage that loads all default workflow templates and validates each resulting `WorkflowDefinition`.

## Dependency Impact

- Subbundle 02 depends on this loader; weak proof here makes seed-service conversion untrustworthy.
- Subbundle 03 depends on the loader tests to prove all default templates are valid, not just present.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Define a small YAML schema for manifest entries, workflow metadata, component prompt data, graph nodes, graph edges, routing, and executor settings.
2. Add the `Templates\Workflows` pack and migrate the current compiled example definitions into YAML.
3. Add a typed loader that resolves the pack root using the same style as `ProcessTemplatePackLoader`.
4. Convert DTOs into `WorkflowGraph` with typed ids, enums, shapes, runtime policies, routing, and executor settings.
5. Add tests that load every template and validate the converted definitions.

## Scope Exceptions

- Hosted marketplace/catalog sharing is not implemented in this subbundle.
- MAF Foundry `AdaptiveDialog` YAML is not adopted as the CanDoItAll runtime schema.

## Do Not Do

- Do not keep default graph topology in compiled helper methods.
- Do not parse YAML into loose dictionaries beyond the boundary needed for deserialization.
- Do not add a silent fallback to compiled defaults.

## Acceptance Checklist

- `Templates\Workflows` exists and contains a YAML manifest plus YAML workflow definitions.
- Loader returns all default examples from files.
- Loader errors include the offending template path or key.
- Every loaded template validates through `WorkflowDefinitionValidator`.

## Proof Required

- Focused unit tests for workflow-template loading and validation.
- Source diff showing no default workflow graph builder remains necessary for loaded defaults.

## Browser Validation Logging

- N/A. Backend/template storage change with no browser-visible behavior.

## Progression Gate

- Subbundle 02 may start only after every default template loads from YAML and validates into a `WorkflowDefinition`.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Add the YAML template pack and typed loader, preserve CanDoItAll's workflow graph model, validate every template, and stop if any default workflow still requires compiled graph construction.
```
