# Generic deferred node completion

## Status

- `Completed`

Closure proof: `proof/SB02/manifest.md` and `proof/SB02/semantic-invariants.md`.

## Objective

Add the generic infrastructure needed to create a canonical project-structure node now and complete slow data/media enrichment later.

## Success Criteria

- A typed deferred completion request model exists.
- Completion runs outside the Blazor page/circuit using fresh scoped services.
- ProjectWorkbenchService can replace media on an existing user-authored node without changing node id, parent, links, or position.
- Failure updates the same node with explicit status/progress/metadata and logs actionable state.

## Covered Inputs

- Generic project-structure delayed data user story.
- Requirements R5, R6, R7, R8, and R9.

## Prerequisites

- SB01 prompt/provider contract gate passed.
- Source references for `ProjectWorkbenchService` and node binding storage are current.

## Exact Source References

- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Workbench/ProjectWorkbenchModels.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectNodes/ProjectNodeBindings.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Workbench/ProjectWorkbenchMetadata.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Services/WorkbenchModuleServiceCollectionExtensions.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/ProjectStructurePageSimpleMutationTests.cs`

## Deliverables

- Typed deferred completion contracts.
- Queue/processor/worker registration.
- Media replacement method on canonical workbench service.
- Focused tests for same-node media replacement and failure state.

## Dependency Impact

- SB03 depends on this subbundle for the generated-image waiting node and completion path. Weak canonicity proof here invalidates the UI flow.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add operational deferred completion metadata that can coexist with node-family metadata.
2. Add a focused media replacement method that saves media through existing storage placement and persists node binding state.
3. Add typed completion request and processor for generated image completion.
4. Add queue/worker infrastructure with DI registration.
5. Add tests for stable node id, media replacement, status/progress, and failure update.

## Scope Exceptions

- Persistent restart-resume job table is deferred unless implementation proves in-process completion cannot meet the request.

## Do Not Do

- Do not bypass `ProjectWorkbenchService` by writing binding rows from the background worker directly.
- Do not use string command payloads for completion kind.
- Do not run provider calls from `ProjectStructurePage` fire-and-forget tasks.

## Acceptance Checklist

- [ ] Same node id after media replacement.
- [ ] Media route/content type/original filename update through binding storage.
- [ ] Failure state is explicit.
- [ ] Queue/worker uses scoped service resolution.
- [ ] No broad graph assembly refactor.

## Proof Required

- Unit/component test transcript.
- Source assertion that `ProjectWorkbenchService` owns media replacement.
- `proof/SB02/manifest.md` and `proof/SB02/semantic-invariants.md` because this is a critical foundation.

## Browser Validation Logging

- N/A for this subbundle. Browser proof is owned by SB04.

## Progression Gate

- Do not start SB03 until same-node media replacement and failure-state proof passes.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
