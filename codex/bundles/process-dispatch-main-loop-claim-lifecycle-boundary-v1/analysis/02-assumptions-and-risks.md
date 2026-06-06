# Assumptions And Risks

## Assumptions

- The branch `maf-processes-refactor` contains the completed projection model/rule decoupling bundle.
- Codex can run `dotnet build CanDoItAll.slnx --no-restore` and focused unit/integration tests locally.
- Runtime/service refactor does not require browser validation.
- Existing broad architecture-test failures caused by stale historical bundle fixture files are unrelated unless this bundle touches those fixtures.

## Critical Path Risks

1. **Claim lease drift**: changing claim acquisition, renewal or release can create duplicate dispatch or stranded claims.
2. **Route order drift**: moving code may reorder database requirement, upstream materialization, stranded recovery, subprocess, start transition, workflow, direct execution or finalizer routes.
3. **Failure closure drift**: exception paths may stop moving steps to Failed or may do so after claim loss.
4. **Heartbeat disposal drift**: losing `finally` semantics can cause leaked heartbeats or incorrect claim release.
5. **Hidden side effects in pure helpers**: EF writes and service-scope calls must be named as coordinators or stores, not hidden inside `Rules` classes.
6. **Over-abstraction**: creating public Core or driver APIs now would lock the wrong shape too early.
7. **Shallow pass**: Codex may only create wrappers and keep all logic in `Dispatch.cs`.

## Validation Risks

- Focused tests may pass while route order changes. Add source-order and behavior tests.
- Build may pass while claim lease semantics change. Add unit/integration tests around claim lifecycle.
- Source scans may miss nested service references. Add negative scans for direct EF claim writes remaining in `Dispatch.cs` outside approved store.

## Reopen Triggers

Reopen the latest production-movement subbundle if any of the following occur:

- `ProcessRunAutomationDispatchService.Dispatch.cs` still owns direct claim EF updates after claim-store extraction.
- Route order is not explicitly tested.
- `ReleaseStepDispatchClaimAsync` semantics are not preserved under exception/finally paths.
- Any Core/driver API appears in production source.
- Any UI/browser/mobile proof files appear.
- Any previous functionality is deleted instead of moved.
