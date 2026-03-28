# Model project hierarchy and persistence foundation

## Status

- `Completed`

## Objective

- Introduce the typed project-to-project hierarchy model, the service/query contract that exposes it, the cycle/self-parent guardrails, and the workbench projection foundation that later UI subbundles can consume without guessing.

## Covered Inputs

- `R001`
- `R002`
- `R003`
- `R004`
- Foundation slice of `R013`
- Raw notes `N001`, `N002`, `N003`, `N004`, `N010`, `N012`

## Prerequisites

- Bundle readiness gate has passed.
- The raw-note coverage matrix still reflects the current scope.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchSchemaInitializer.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectsServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`

## Deliverables

- A persisted hierarchy relation model for projects.
- Typed service/query methods for direct parents, direct children, and traversal metadata.
- Explicit cycle and self-parent rejection.
- A workbench structure-surface projection contract that can surface related project nodes later.
- Integration tests that prove the new contract.

## Dependency Impact

- This is a critical foundation. If it is wrong, the Projects page, the canvas projection, and the final closure audit become untrustworthy.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add the project-hierarchy persistence model and configuration in the Projects module.
2. Extend `ProjectsService` with typed hierarchy query and mutation methods plus explicit cycle/self-parent validation.
3. Extend workbench structure sync so related project data can be projected later without abusing the old single-parent node contract.
4. Add or update integration tests for persistence, traversal, cycle rejection, and structure projection.
5. Prove one dependent lookup or structure-surface smoke before allowing UI subbundles to start.

## Scope Exceptions

- This phase does not close the visible Projects page or canvas UX by itself. It exists to make those later phases technically sound.

## Do Not Do

- Do not bolt hierarchy state onto the page as ad-hoc client-side joins.
- Do not use stringly typed error handling for invalid relations.
- Do not overload `ParentNodeKey` as the sole truth for multi-parent project relations.

## Acceptance Checklist

- A project can have many parents and many children in persisted data.
- Self-parent and cyclic relation attempts fail explicitly.
- The service contract exposes enough hierarchy metadata for `/projects` and `/projects/{id}/structure`.
- Existing non-hierarchy project save behavior still works.
- A structure-surface fetch for a related-project scenario returns data that later UI phases can consume without inventing another model.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectsServiceIntegrationTests|FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests"`
- A dependent smoke proving the returned hierarchy data or structure surface includes the new relation semantics needed by subbundles 02 and 03.
- No browser proof is required in this phase because the user-visible hierarchy surfaces do not exist yet.

## Browser Validation Logging

- `N/A`
- This phase changes the data contract and service foundation only. Browser truth begins once visible hierarchy UI lands in subbundles 02 and 03.

## Progression Gate

- The targeted integration tests pass.
- Cycle/self-parent guardrails are proven.
- The structure-surface contract is strong enough that subbundles 02 and 03 can consume it without redefining the data shape.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Add the typed project hierarchy persistence and query foundation, reject self-parent and cycle errors explicitly, and make the workbench projection contract ready for later UI phases. Do not start visible UI work here. Prove the foundation with targeted integration tests and one dependent hierarchy smoke before updating the gate result.
```
