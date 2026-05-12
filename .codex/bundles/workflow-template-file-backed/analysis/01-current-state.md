# Current State

## CanDoItAll Workflow Defaults

- `WorkflowExampleCatalogSeedService` currently owns default workflow examples in compiled code through `BuildExampleSpecs()` and graph-builder methods.
- Each default example creates an `Example LLM: ...` component with shared JSON shapes, model settings, permissions, and prompt contract.
- Default workflow definitions are saved as active managed examples named `Example: ...`; seed refresh is controlled by a `SeedMarker` and `SeedVersion` embedded in the description.
- The service also seeds sample workspace files under `samples/workflows`, including markdown files and workbook fixtures.

## Existing File-backed Template Precedent

- Process templates already live under `Templates\Processes` with a manifest, per-process definitions, and a loader.
- `ProcessTemplatePackLoader` resolves the pack root from explicit configuration, repository-relative defaults, current directory, app base directory, and assembly location.
- This is the closest local pattern for workflow template storage because it keeps templates text-based and runtime loading strongly typed.

## MAF Reference

- MAF declarative workflows are YAML files loaded through `DeclarativeWorkflowBuilder.Build(...)`.
- MAF's YAML schema describes Foundry object-model workflows, not CanDoItAll's current workflow definition graph.
- The useful pattern is file-backed YAML loading and validation, not replacing CanDoItAll's current domain model in this slice.
