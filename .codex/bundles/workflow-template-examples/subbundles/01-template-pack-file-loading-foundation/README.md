# 01-template-pack-file-loading-foundation

## Status

- `Completed`

## Objective

- Establish the external workflow template file loading foundation by updating the workflow template manifest, preserving existing example keys, and adding test coverage that proves new manifest-listed files load through `WorkflowTemplatePackLoader`.

## Success Criteria

- New workflow template files are referenced from `Templates\Workflows\manifest.yaml`.
- `seedVersion` changes so managed seeded examples can refresh.
- Existing workflow keys remain available.
- A targeted test can load the manifest and compile representative graphs.

## Covered Inputs

- R1, R2, R8 foundation coverage.
- Raw note `N005`: templates must not be hard-coded in code.

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\Templates\Workflows\manifest.yaml`
- `C:\repositories\CanDoItAll\Templates\Workflows\workflows\default-workflows.yaml`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowTemplatePackLoader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowExampleCatalogSeedService.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs`

## Deliverables

- Manifest update with new workflow file references.
- Seed version bump.
- Test helper/assertions for loading new and existing template keys.

## Dependency Impact

- Subbundles 02 and 03 depend on this foundation. If manifest loading is wrong, their template files can exist but never become seeded workflow examples.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Add new manifest `workflowFiles` entries for email plugin tasks and file-analysis examples.
2. Bump manifest version and seed version.
3. Add or extend a unit test that loads the pack and proves existing summary keys plus new keys are present.
4. Run targeted test command.

## Scope Exceptions

- This subbundle does not author the actual new workflow nodes; subbundles 02 and 03 own those templates.

## Do Not Do

- Do not move or delete existing workflow templates.
- Do not add hard-coded C# workflow graph definitions.
- Do not change plugin runtime behavior.

## Acceptance Checklist

- `manifest.yaml` includes new external workflow file paths.
- Existing summary keys still load.
- Targeted tests include new template keys or fail clearly before downstream phases proceed.

## Proof Required

- Targeted `dotnet test` command covering the loader assertions.
- Execution report row updated with command outcome.

## Browser Validation Logging

- N/A - data/template pack change with unit-test proof.

## Progression Gate

- Downstream subbundles may continue only after the manifest loads without duplicate-key errors and the test can observe the new file-backed template keys.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
