# Assumptions, Risks, and Reopen Triggers

## Assumptions
- `maf-processes-refactor` remains the active implementation branch.
- The latest live OpenAI process-run proof is valid but must remain opt-in, budget-bounded, and secret-safe.
- PostgreSQL availability can vary; EF audit persistence must have deterministic setup and failure classification.
- Verification host production-readiness is the current target; execution-capable drivers are not approved in this bundle.
- Large desktop proof is sufficient for the current UI validation stage.

## Critical Path Risks
- EF audit store is registered but not truly included in EF model/migrations/profile bootstrap.
- Sync compatibility paths silently become production paths.
- Host options are documented but not enforced in manager/scheduler/workflow paths.
- Scheduler/workflow verification jobs invoke drivers directly instead of the process-owned verification host boundary.
- Manager readback omits audit hash, denial category, no-mutation flags, or evidence counts.
- Sandbox terminology gets mistaken for approved execution.
- Future execution driver contracts leak into Process Core.

## Validation Risks
- Report rows can pass while source uses in-memory audit or sync wrappers.
- Green deterministic tests can mask live-provider or runtime-host policy gaps.
- Browser screenshots can prove the old run detail but not new manager verification diagnostics.
- Source scans can miss dynamically composed hooks or test-only fallback paths.

## Reopen Triggers
- Production source reads `codex/bundles/<name>`.
- Process Core references driver packages, Modules, Infrastructure, EF, UI, OpenAI, HTTP, workspace, storage, or MAF.
- Verification host starts allowing process/transition/finalizer/retry/claim mutation.
- EF audit records are not queryable after new service scope and app/profile restart.
- Scheduler/workflow jobs bypass `IProcessVerificationRuntimeHost`.
- Live OpenAI test is skipped but reported as provider proof.
- UI readback lacks audit id/hash, denial category, no-mutation flags, or evidence reference counts.