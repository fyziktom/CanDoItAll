# SB05 Runtime And Dispatcher

## Status

Planned.

## Objective

Build the runtime engine and dispatcher around persisted instance plans, typed state transitions, leases, cancellation, and event emission.

## Covered Inputs

- REQ-002
- REQ-003
- REQ-020
- REQ-026
- REQ-027

## Prerequisites

- SB02 complete.
- SB04 complete.
- Basic SB06 strategy contracts complete.

## Exact Source References

- `bundle://analysis/02-runtime-dispatcher-insufficiency.md`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- `repo://src/CanDoItAll.Processes.Core/Routing`

## Deliverables

- `CanDoItAll.Processes.Runtime`
- Runtime scheduler.
- Dispatcher queue and claim lease.
- Strategy invocation pipeline.
- Transition invariant service.
- Runtime event publisher.
- Outbox/inbox persistence adapter.

## Dependency Impact

- Artifact recovery, monitoring, and UI depend on reliable runtime events and transitions.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Add runtime state machine.
2. Add scheduler eligibility rules.
3. Add dispatcher claim and lease renewal.
4. Invoke assigned step execution strategy.
5. Normalize execution results.
6. Apply transitions through runtime only.
7. Emit typed events.
8. Handle cancellation, failure, timeout, and lease expiry.

## Scope Exceptions

Domain-specific execution strategies can be stubs only if they fail explicitly and are covered by negative tests. No silent fallback.

## Do Not Do

- Do not reintroduce a mega dispatcher.
- Do not mutate runtime state from strategies.
- Do not make monitoring synchronous with execution.

## Acceptance Checklist

- Concurrent dispatch tests prove single claim per step.
- Runtime transition tests cover success, failure, block, cancel, and retry.
- Events are emitted for every state transition.
- Strategies cannot bypass runtime transitions.

## Proof Required

- Unit and integration transcripts.
- Concurrency stress transcript.
- Semantic Adequacy Gate.
- `proof/SB05/manifest.md`.
- Production Behavior Artifact Matrix for runtime state, claim lease, and events.

## Browser Validation Logging

- N/A.

## Progression Gate

- SB07 and SB08 require stable runtime events and transition semantics.

## Suggested Agent Prompt

Implement a small runtime kernel plus dispatcher. Strategy execution is pluggable; transitions are centralized.
