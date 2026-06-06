# Assumptions And Risks

## Assumptions

- Branch under implementation: `maf-processes-refactor`.
- No UI source changes are expected.
- Current successful behavior must remain unchanged.
- Existing focused integration tests are acceptable closure proof for moved dispatch behavior when the full integration run exceeds the command window.
- Production Process Core and production process-driver APIs are intentionally deferred.

## Critical Path Risks

1. **Adapter burn-down can accidentally move infrastructure logic too far.**
   - EF, storage, workflow execution, agent execution, and claim lease operations must remain application-local.

2. **Route model snapshots can lose live mutation semantics.**
   - Route candidate refresh and direct-agent execution outcome must still update the correct runtime state.

3. **Hydration split can drop direct-agent binding behavior.**
   - Project-structure read access granting, provider assignment, manual recovery directive lookup, and recoverable execution run selection must remain intact.

4. **Subprocess projection can silently lose artifact lineage or gap journaling.**
   - Source artifact selection, gap journal, parent-scoped artifact write, and save changes must remain proven.

5. **Finalizer abstraction can hide transition side effects.**
   - Finalizer context construction can be isolated, but step transition application remains application-local.

## Validation Risks

- A build-only proof is insufficient.
- Focused tests must cover route order, claim lease, route services, hydration, subprocess, finalizer application, and projection.
- Source scans must reject `CanDoItAll.Processes.Core`, `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, and production driver namespaces.
- Source scans must reject UI/media changes and small/medium/mobile proof artifacts.

## Reopen Triggers

- Any route handler changes stage order.
- Any moved service drops claim-held checks.
- Any service starts depending directly on `CanDoItAll.Processes.Core`.
- Any production driver API is introduced.
- Any direct dispatcher nested type usage leaks outside approved adapter files.
- Any proof says “passed” but lacks line-count/source/test transcript.
