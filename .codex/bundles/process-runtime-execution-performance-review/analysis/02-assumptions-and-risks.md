# Assumptions And Risks

## Assumptions

- Runtime start is a hot path for process execution, especially when generated definitions contain many steps and role requirements.
- Most LINQ in the module is acceptable outside hot paths; this bundle should not perform broad style rewrites.
- Process UI refresh performance was already handled separately and should not be reopened without a new measured signal.

## Critical Path Risks

- Assignment selection has precedence rules: step-scoped assignments beat run-scoped assignments, assigned parties beat gaps, and first equivalent row wins. Any optimization must preserve that ordering.
- Role requirement ordering affects executor selection. Replacing LINQ with loops must preserve responsibility priority, fallback order, and required-first fallback behavior.
- Process core must remain generic and must not gain knowledge of .NET build, test, or app archetype instructions.

## Validation Risks

- Timing-based tests can be flaky on local developer machines; behavior-preserving tests plus command timings in the execution report are safer than hard thresholds.
- Mock-agent end-to-end tests can be slower than runtime-only tests; if they time out, record the gap and run narrower dispatch tests.
- Independent .NET app build smoke cases prove the local build path, not process semantics.

## Reopen Triggers

- Any targeted process integration test fails after the runtime-start changes.
- Assignment display names, capability gap severity, work brief evidence summaries, or ready/pending step states differ from current tests.
- Mock-agent dispatch dead-letters after the optimization.
- A simple .NET app build smoke case fails because process core now assumes stack-specific instructions.
