# SB015 Semantic Invariants

## Raw Note Closure
- Raw note owned: preserve process behavior while moving transition shape decisions into pure Core facts and module adapters.
- Literal closure: start/block/mirror transition reason, target status, decided-by, concurrency token, and suppress flags match existing behavior.

## Shallow-Pass Trap
- A shallow pass would add a Core record but still construct requests directly in planners, or fail to prove request field parity.
- This gate requires architecture proof, focused transition tests, full dispatch integration proof, build proof, source assertions, and Core forbidden-token scan.

## Semantic Positive Proof
- `ProcessDispatchStartTransitionPlanner_SB10_INV_001_builds_start_request_without_executing_transition` asserts start intent fields, adapted request fields, and legacy planner request fields are equal.
- `ProcessSubprocessLifecycleRules_SB05_INV_001_preserves_transition_field_parity` asserts subprocess start/block/mirror transition request shape remains unchanged.
- `ProcessRunAutomationDispatchServiceTests` passed with 536 tests.

## Adversarial Negative Proof
- `Process_core_stabilization_SB013_SB014_INV_001_keeps_transition_intents_pure_and_adapter_owned` proves Core does not reference `ProcessStepTransitionRequest` or `TransitionStepWithClaimAsync`.
- `bundle://proof/SB015/transcripts/core-transition-forbidden-token-scan.txt` proves no transition execution, module, infrastructure, scope-factory, or driver tokens leaked into Core.

## Anti-Stub Audit
- `bundle://proof/SB015/transcripts/anti-stub-audit.txt` found no TODO, NotImplemented, stub, or fixture-specific markers in changed transition-intent production files.

## Boundary Proof
- `ProcessTransitionIntentAdapters` is the module-owned translation point from Core transition facts to `ProcessStepTransitionRequest`.
- No UI, browser, mobile, or media files were changed.
