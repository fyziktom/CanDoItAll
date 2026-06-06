# Current State Analysis

## What the previous bundle achieved
The previous bundle made meaningful progress. The execution report states `Completed`, with 27 broader subbundles closed. It moved more real work than the earlier micro-subbundle attempts:
- route services no longer contain dispatcher adapter leakage,
- candidate hydration moved into `ProcessDispatchCandidateHydrationService`,
- subprocess orchestration moved into `ProcessDispatchSubprocessRuntimeService`,
- finalizer application behavior moved into `ProcessDispatchFinalizerApplicationService`,
- dispatcher file sizes are significantly lower,
- build, full unit tests, focused dispatch integration tests, and focused subprocess/projection/execution-client tests passed.

## Why Process Core should still wait
The current architecture is cleaner, but Core extraction is still premature because some models and services still bridge to dispatcher-owned types or full application/infrastructure details:
- route models still keep hidden `Source` payloads for compatibility,
- `ProcessRouteExecutionOutcome` still carries `ProcessAutomationExecutionRunDetail`,
- direct-agent runtime still adapts back to dispatcher candidate/outcome,
- finalizer application service still uses dispatcher aliases and delegates,
- subprocess runtime still mixes lifecycle orchestration and artifact projection persistence,
- hydration still combines EF query, artifact input shaping, recovery, binding, project access mutation, and cooperation metadata.

## Recommended next strategy
Do one broad pre-Core isolation pass across multiple areas:
1. burn down source payloads/adapters,
2. split hydration side effects,
3. split subprocess projection persistence,
4. slim execution/finalizer DTOs,
5. align projection/validation DTOs,
6. identify safe pure-rule Core candidates,
7. prepare driver-readiness docs without adding driver APIs,
8. close with a real Core readiness decision.
