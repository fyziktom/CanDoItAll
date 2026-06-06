# Current State Analysis

## What is good now

- The previous MAF/process decoupling line is stable enough to continue local dispatcher isolation.
- Artifact projection has an orchestrator and source-family coordinators.
- Broad `IProcessArtifactProjectionHost` has been replaced by focused projection facets.
- The prior proof says source-family order is preserved and no Process Core or production driver API was introduced.

## Main remaining coupling

`ProcessArtifactProjectionFacetImplementations.cs` still imports aliases to nested `ProcessRunAutomationDispatchService` models:

- `DispatchCandidate`
- `DispatchArtifactExpectation`
- `ProcessMockArtifactProjection`
- `ProcessStepDispatchClaim`
- `SessionFileContent`
- `ArtifactProjectionLineage`

Many facet implementations still forward directly to static methods on `ProcessRunAutomationDispatchService`. This is a safe transitional state but still prevents clean extraction of process-core-like logic later.

## Why this bundle is not Process Core

The current projection boundary still depends on module runtime concepts, workspace paths, storage placement, execution artifacts, process mock artifacts, browser proof, and mutable candidate state. These need to be translated into stable module-local projection models before any Core split can be considered.

## Next cutline

Create projection-specific read models and rule helpers inside `CanDoItAll.Modules.Processes` only. Keep all translation from dispatcher nested models in one adapter boundary. Do not move EF entities, process run lifecycle, dispatch claims, or storage services into a new project.
