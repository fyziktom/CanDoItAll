# Assumptions and Risks

## Assumptions

- The latest branch is `maf-processes-refactor`.
- The previous bundle completed and source scans are trustworthy.
- The route order in `ProcessDispatchRoutePipeline.StageOrder` is the canonical order.
- Existing focused tests cover at least claim, route order, projection, subprocess and dispatch happy/negative paths.

## Critical Path Risks

- Route order drift can silently change process behavior.
- Transition side effects may be hidden inside "pure" helpers.
- Finalizer handoff can be reordered before competing/run-closed guards.
- Subprocess route may lose projection or capability-gap handling.
- Start transition reload fallback may be weakened.
- Failure closure can accidentally skip failure transition when claim is still held.
- Production driver API may appear too early and freeze the wrong abstractions.
- Process Core extraction may be started prematurely.
- Collapsed execution-report rows may hide skipped subbundles.
- Codex may create wrapper-only handlers while leaving real decisions in `RouteExecution.cs`.

## Validation Risks

- Focused tests may pass while route order changes in production source.
- Source scans may be too broad and miss handler order.
- Build-only proof is insufficient.
- The broad architecture test class may still have unrelated stale fixture failures; those must be documented separately, not used to waive scoped proof.

## Reopen Triggers

Reopen the current or prior subbundle if:
- `ProcessDispatchRoutePipeline.StageOrder` changes without explicit proof.
- Any route handler is skipped, duplicated or reordered.
- `RouteExecution.cs` retains full route bodies after handler extraction.
- Any transition/finalizer/execution call moves into a class named `Rules`.
- `CanDoItAll.Processes.Core` appears.
- `IProcessDriverPack`, `ProcessDriverRegistry` or related production driver API appears.
- UI/mobile/browser proof files are created.
- Execution report collapses rows instead of recording each subbundle.
