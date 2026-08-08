# Final merge gate

The branch is mergeable only when all of the following are true.

## Static architecture

- MAF project has no `Modules.*` references.
- Runtime abstractions reference Models only.
- LLM abstractions remain agent/workspace/process/MAF free.
- Ordinary conversation projects remain MAF free.
- No production `AddLlmConversations` registration remains.
- Current turn metadata cannot degrade from malformed authority to no governance.

## Behavioral proof

- Cross-instance file-store CAS admits exactly one winner.
- Failed provider adoption restores provider and acceleration state.
- Rename during active turn fails typed without changing revision.
- Near-capacity turn fails before the provider call.
- Empty-response retry aggregates usage.
- Workflow failure usage uses typed accumulated usage.
- Canvas -> Gantt creates a new turn snapshot without changing an in-flight turn.
- Approval continuation keeps the original authority and scope.
- Per-proposal mixed approval decisions remain supported.
- Durable process-lease cleanup remains green.

## Regression

Record exact commands, test counts, failures, and comparison against `development`. No unexplained
baseline update is acceptable.
