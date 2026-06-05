# Assumptions And Risks

## Assumptions

- Existing focused tests cover subprocess route behavior enough to preserve parity, but Codex must inventory and extend them before source movement.
- `WorkflowSubprocessArtifactMapper` remains the source of child artifact mapping semantics.
- File/DB writes must stay explicit and observable.
- Existing `ProcessArtifactProjectionWriteCoordinator` is not automatically safe to reuse for subprocess projection because subprocess currently writes parent-scoped markdown directly and records artifacts/journal entries in a specific flow.

## Critical Path Risks

- Accidentally changing parent artifact `ExternalReferenceKey` format.
- Moving SaveChanges timing and changing partial projection persistence behavior.
- Hiding `EnsureSubprocessRunForStepAsync` and transition calls in a helper that looks pure.
- Losing capability-gap block behavior for active child steps with missing executors.
- Removing or weakening manager/finalizer validation after subprocess completion.
- Creating Process Core or driver APIs prematurely.

## Validation Risks

- Compile-only proof would miss semantic drift.
- Broad tests may pass while subprocess-specific projection edge cases break.
- Source scans must distinguish allowed side-effect coordinators from forbidden pure helpers.

## Reopen Triggers

Reopen earlier subbundles if:

- any subprocess artifact key format changes;
- route order changes relative to pre-execution guard / subprocess / workflow / agent execution;
- Process Core or driver APIs appear;
- any UI file changes;
- helper extraction only moves names but keeps duplicate logic in `Dispatch.cs`;
- focused subprocess tests are missing or skipped.
