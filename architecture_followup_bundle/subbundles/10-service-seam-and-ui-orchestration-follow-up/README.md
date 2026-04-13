# Service seam and UI orchestration follow-up

## Purpose

After the invariants are safe, reduce the remaining concentration in `ProcessesService` and `ProcessWorkspace` so future growth does not re-centralize the module.

## Required deliverables
- Injectable read/query seams instead of nested static helper instances.
- A thinner `ProcessesService` façade or a split command/read service arrangement.
- Reduced workspace orchestration concentration where practical without destabilizing behavior.
- Updated component tests and, if UI structure changed materially, refreshed browser proof.

## Repository touchpoints
- `src/CanDoItAll.Modules.Processes/ProcessesService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.QueryServiceFields.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.DefinitionListQuery.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.RuntimeReadQuery.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Presenters.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsTab.razor`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceStepsTab.razor`
- `tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`

## Validation commands
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`

## Review questions
1. Are query seams now injectable and independently testable?
2. Did the service/UI follow-up reduce responsibility concentration instead of merely moving code between partials?
3. Did structural cleanup preserve the already-hardened invariants and behavior?

## Corrective trigger

If structural work starts re-concentrating logic or destabilizes the hardened invariants, stop and open the structure corrective playbook before final closure.

## Corrective template

- `subbundles/_corrective-structure-reset`

## Detailed execution notes

This is intentionally the last implementation phase. Do not spend early bundle time here while red invariants are still open.
