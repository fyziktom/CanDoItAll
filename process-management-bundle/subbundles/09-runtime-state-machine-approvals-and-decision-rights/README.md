# 09 Runtime State Machine, Approvals, And Decision Rights

## Status

- `Completed`

## Objective

- Implement the governed runtime core:
  runs, step runs, claims, approvals, escalations, variants, exceptions, refusal outcomes, and explicit decision-right rules.

## Covered Inputs

- `REQ-006`
- `REQ-010`
- Raw notes `N01`, `N05`, and `N06`
- Legacy features `PRM-F05`, `PRM-F06`, `PRM-F07`, and `PRM-F18`

## Prerequisites

- `08-post-implementation-bundle-phase01-generation`

## Exact Source References

- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F05-transition-rules-decisions-and-explicit-handoffs\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F06-approval-policies-escalations-and-governance-gates\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F07-runtime-execution-state-machine-and-assignments\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F18-variants-exceptions-input-quality-and-decision-rights\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\02-architecture\12-process-native-orchestration-and-baton-handoffs.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.SharedKernel`

## Deliverables

- Canonical runtime state model for process runs and step runs.
- Typed transition, approval, escalation, exception, and refusal outcomes.
- Assignment-resolution rules that respect role requirements, eligibility, fallback, and governance limits.
- Operating-mode and autonomy-envelope hooks attached to runtime decisions.

## Dependency Impact

- Work briefs, journals, bridge contracts, metrics, and conformance all depend on this state model.
- If this subbundle is wrong, every later runtime, analytics, and audit proof becomes suspect.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Implement process run and step run state transitions.
2. Add approval, escalation, and decision-right enforcement.
3. Add input-quality, exception, and refusal paths.
4. Connect assignment-resolution logic to role-first staffing semantics.

## Scope Exceptions

- External AgentFramework execution remains deferred; this subbundle models the process-side runtime and policy core only.

## Do Not Do

- Do not encode approval or refusal logic only in UI or prompts.
- Do not allow ambiguous or conflicting runtime states.
- Do not turn role resolution into fixed executor wiring.

## Acceptance Checklist

- Valid runtime transitions are explicit and tested.
- Approval and escalation outcomes are journalable and governable.
- Refusal and exception outcomes are first-class states, not generic failures.
- Assignment resolution respects eligibility, fallback, and policy.

## Proof Required

- Integration tests for runs, approvals, escalations, and exception paths.
- At least one dependent-flow smoke after closure because this is a critical foundation.
- Review evidence that operating-mode and autonomy envelope context is preserved in runtime decisions.

## Browser Validation Logging

- Route:
  runtime or run-detail surfaces if introduced in this phase
- Viewport:
  `1920x1080`
- Evidence:
  only required when a browser-visible runtime surface lands in the same subbundle

## Progression Gate

- Downstream trust, journal, and analytics subbundles may start only when runtime state, approvals, and refusal paths are deterministic and tested.

## Suggested Agent Prompt

```text
Implement only the governed process runtime core. Make runs, approvals, decision rights, exceptions, and refusal outcomes explicit and typed, and keep assignment resolution aligned with role-first staffing semantics.
```
