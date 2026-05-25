# SB03-project-structure-mutation-latency

## Status

- `Completed`

## Objective

Reduce add-node latency in Project Structure by patching the current canvas surface after successful persistence instead of reloading the complete assembled structure for the normal existing-surface path.

## Success Criteria

- The created node appears and is selected without a full `ReloadSurfaceAsync` call.
- Hierarchy links, user-authored pending links, and follow-up move positions are reflected locally.
- Existing inline-update no-reload behavior remains intact.
- A component test proves the create path uses fewer DbContext creations than the prior full-reload path.

## Covered Inputs

- `REQ-PROJ-001`

## Prerequisites

- `SB01` complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `repo://src/CanDoItAll.Modules.Workbench/Workbench/ProjectWorkbenchModels.cs`
- `repo://tests/CanDoItAll.Tests.Components/ProjectStructurePageSimpleMutationTests.cs`

## Deliverables

- Local `ProjectStructureSurface` create-patch helper.
- Test coverage for quick node insertion and reduced reload work.

## Dependency Impact

- Final web validation depends on this phase because Project Structure should remain responsive after create operations.

## Validation Depth

- `Critical UI mutation foundation`

## Implementation Steps

1. Inspect surface and link model constructors.
2. Add a local-patch helper for created nodes.
3. Apply follow-up move coordinates locally after persistence succeeds.
4. Preserve full reload only for the explicit no-current-surface case.
5. Update component tests.

## Scope Exceptions

- This phase does not redesign Project Structure persistence services.
- This phase does not change graph layout algorithms beyond reflecting already-computed create placement.

## Do Not Do

- Do not invent a parallel client-side source of truth.
- Do not skip persistence before showing the created node.

## Acceptance Checklist

- Persisted created node is added to `surface.Nodes`.
- Required links are added to `surface.Links` without duplicates.
- Existing nodes affected by follow-up move requests are updated locally.
- Selected node state targets the created node.

## Proof Required

- Targeted component test command covering `ProjectStructurePageSimpleMutationTests`.
- Relevant build proof in `SB05`.

## Browser Validation Logging

- Target route: Project Structure page during final web-app startup if available.
- Viewport passes: N/A unless layout changes are introduced.
- Playwright actions or assertions: N/A unless layout changes are introduced.
- Screenshot evidence: N/A unless layout changes are introduced.
- Review questions: confirm no layout-affecting markup changes were made.

## Progression Gate

- Project Structure component test must pass before final validation.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
