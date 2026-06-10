# Gap Analysis Toward Stable Generic Runtime Host and Process Execution

## Product runtime gaps
- Need direct proof that real process templates still execute end-to-end after refactor.
- Need template catalog inventory for software development, Blazor/.NET, business analysis, and multi-team development if present.
- Need project-structure launch proof that reaches artifacts and run detail, not just launch-plan creation.
- Need manager/operator readback of runtime-host verification/dry-run results in the context of a real process run.

## Runtime-host gaps
- Contracts are improving but still thin around lifecycle, correlation, and job execution.
- Dry-run execution pipeline exists but is not yet integrated as a first-class process-manager/runtime diagnostic path.
- Scheduler/workflow read-only job runner lacks persisted lifecycle and observable status.
- Durable audit needs stronger query/retention/index/readback proof, ideally across new scopes and after process runtime execution.
- Execution-capable drivers remain blocked; next work should prepare gates and dry-run plans, not execute effects.

## Refactor regression risks
- Process templates may be visible but stale or not executable.
- Dispatch may work in fake/deterministic tests but fail for full template graphs.
- Runtime-host driver concepts may leak into Process Core if not scanned.
- Future generic host work can accidentally introduce reflection discovery, fallback selector, or driver self-registration.
