# True canonical dependency model closure

## Purpose

Remove legacy dependency mirrors from the core Process model and keep any old-format compatibility strictly at import/export boundaries.

## Required deliverables
- Core Process entity/editor/runtime models that expose only canonical dependency collections/rows.
- Versioned compatibility DTOs/adapters for old import/export payloads if backward compatibility is still required.
- Removal of `FirstOrDefault()` single-dependency shortcuts from runtime projections and UI consumption paths.
- Fresh tests proving old envelope compatibility does not reintroduce core dual representation.

## Repository touchpoints
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntities.cs`
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEditorModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeViewModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessDependencyCompatibilityBridge.cs`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasBranching.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.ImportNormalization.cs`
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionDraftCloneEngine.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.RuntimeReadQuery.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Persistence.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`

## Validation commands
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`

## Review questions
1. Is there now exactly one dependency representation inside the core module types?
2. Is old-format compatibility isolated to explicit boundary adapters instead of bridges on core models?
3. Did runtime/UI paths stop depending on a single-primary-dependency shortcut?

## Corrective trigger

If any legacy dependency mirror remains on a core entity/editor/runtime type, or if compatibility still mutates core models, fail the gate and open the canonicality corrective playbook first.

## Corrective template

- `subbundles/_corrective-canonicality-reset`

## Detailed execution notes

- Remove scalar dependency mirror fields from:
  - `ProcessStepDefinition`
  - `ProcessStepEditorModel`
  - `ProcessStepRunViewModel`
- Replace the current bridge with one of these two patterns:
  - remove it entirely if no compatibility input remains; or
  - narrow it to a versioned import/export adapter that is not referenced by core service/query/UI code.
- Update runtime reads so consumers use `Dependencies` only.
- Update import/export so old envelopes map into canonical dependency collections before validation/save.
- Prefer schema cleanup as part of this subbundle if the legacy columns are no longer needed.
