# Assumptions And Risks

## Assumptions

- The current branch builds and passes the previous bundle's focused test gates.
- Step finalization is still process-module-owned application/runtime logic, not Process Core.
- Content readers and pure value types can be extracted locally without changing behavior.
- Final transition mutation must remain in dispatcher-owned orchestration for this bundle.
- Driver preparation should remain documentation/readiness only.

## Critical Path Risks

- Moving finalizer context/result records too aggressively could cascade into broad refactors.
- Moving content readers could accidentally change storage-reference fallback semantics.
- Moving transition request construction could drop artifact-validation context fields.
- Moving runtime invariant audit could accidentally change severity/blocking behavior.
- Introducing driver APIs before a stable finalizer vocabulary exists would encode the wrong boundary.

## Validation Risks

- Compile-only proof could miss subtle differences in blocked/completed/manager-recovery behavior.
- Line-count reduction alone is not a correctness signal.
- A helper that reads files/storage directly can become a hidden side-effect seam unless scans catch it.
- Tests must cover satisfied, missing, stale/wrong-run, content unavailable, hash mismatch, wrong-producer, and manager-recovery cases.

## Reopen Triggers

- Any new Process Core or process-driver production project appears.
- Any helper references MAF, Tooling, Workbench, Projects, Razor, DbContext, or storage side effects outside its allowed boundary.
- Final transition request loses artifact-validation executor/run/recovery context.
- Manager artifact recovery no longer revalidates after recovery.
- Runtime invariant violations no longer block completed steps when severe.
- Any small/medium/mobile proof artifact appears.
