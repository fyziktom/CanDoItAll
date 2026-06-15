# Manager Runtime And Control Loop

## Design Intent

The manager supervises process execution but must not become a second dispatcher. It interprets events, incidents, artifacts, branch requests, and subprocess messages, then emits decisions through runtime-controlled transitions. It never mutates runtime state directly.

Manager behavior can be deterministic, agent-backed, or hybrid. In all cases deterministic policy owns approvals, budgets, idempotency, access checks, and escalation boundaries.

## Manager Invocation Triggers

Manager invocation can be triggered by:

- strategy result envelope with manager signal,
- step blocked event,
- missing/stale/invalid artifact event,
- branch decision request,
- recovery request completion,
- subprocess control message,
- user response,
- approval response,
- loop budget threshold,
- projection/operator command requiring manager interpretation,
- runtime health anomaly.

## Control Loop

1. Runtime emits event or creates manager work item.
2. Manager queue stores work item with correlation ID, causation event, idempotency key, priority, and sensitivity.
3. Manager runtime loads instance plan, runtime snapshot, recent events, relevant artifacts, budgets, policies, and driver facets.
4. Error preprocessing strategy converts raw diagnostics to restricted evidence plus user-safe incident content.
5. Policy engine evaluates allowed actions.
6. Manager strategy proposes decision.
7. Deterministic policy validates proposal.
8. Manager records decision event.
9. Runtime applies resulting transition or schedules dispatcher-controlled recovery execution.
10. Projection workers update UI-facing incident and status views.

## Manager Decision Idempotency

Manager decisions use stable idempotency keys:

- incident ID plus decision purpose,
- branch request ID plus selected outcome,
- recovery attempt ID,
- subprocess message ID,
- user response ID.

Duplicate manager execution returns the existing decision if inputs and plan hash match. If inputs changed, the manager creates a superseding decision linked by causation ID.

## Incident Lifecycle

```text
Raised -> Classified -> AwaitingPolicy -> Recovering -> Resolved
Raised -> Classified -> WaitingForUser -> Recovering -> Resolved
Raised -> Classified -> Escalated -> WaitingForUser -> Resolved
Raised -> Classified -> Failed
```

Incident records include:

- source event ID,
- affected run/step/artifact IDs,
- classification,
- severity,
- raw diagnostic refs,
- user-safe summary,
- allowed actions,
- policy decision,
- budget status,
- escalation owner,
- sensitivity,
- resolution event.

## Policy Evaluation Order

1. Security and access policy.
2. Sensitivity/redaction policy.
3. Approval policy.
4. Budget policy.
5. Idempotency/repeat-safety policy.
6. Artifact access/freshness policy.
7. Driver/domain policy facets.
8. Escalation policy.

If any required policy denies the action, manager records denial and chooses an allowed alternate action or escalates.

## Recovery Request Lifecycle

```text
Requested -> Approved -> Scheduled -> Running -> Completed -> Applied
Requested -> Denied -> Escalated
Running -> Failed -> Retriable
Running -> Failed -> Escalated
```

Recovery execution is handled through dispatcher/strategy execution or a manager-controlled strategy executor that still returns envelopes. Recovery cannot directly mutate runtime state or artifacts without runtime ledger transitions.

## Subprocess Message Handling

Parent/child managers exchange durable messages:

- artifact projection request,
- artifact projection accepted/rejected,
- incident raised,
- escalation raised,
- cancellation requested,
- completion summary,
- recovery coordination request.

Messages are not method calls. They are persisted, correlated, and handled by manager work items.

## Escalation Behavior

Escalation is required when:

- automatic recovery budget is exhausted,
- loop fingerprint repeats beyond threshold,
- approval is required and missing,
- manager policy denies safe automatic action,
- strategy implementation is unavailable,
- raw diagnostic sensitivity prevents automatic handling,
- subprocess manager cannot resolve child incident.

Escalation produces a user-facing incident projection and an audit event.

## Anti-Patterns

- Manager invokes agents/workflows directly outside strategy/adapters.
- Manager updates step status directly.
- Manager fabricates artifacts without ledger events.
- Manager chooses branch outcomes without decision event.
- Manager catches all errors and retries silently.
- Manager contains domain-specific `if` branches that should be driver strategies.

## Invariants

- Every manager decision is an event.
- Every automatic recovery consumes budget before execution.
- Agent-backed output is policy-checked before action.
- Manager cannot bypass runtime transition validation.
- User-facing incidents are sanitized.
- Raw diagnostics remain restricted evidence.

## Failure Behavior

| Failure | Required response |
| --- | --- |
| Manager strategy unavailable | Runtime incident and escalation to operator. |
| Manager output violates policy | Decision denied event and alternate/escalation path. |
| Duplicate manager work item | Idempotency returns existing decision. |
| Manager loop repeats same failed recovery | Fingerprint budget escalates. |
| Subprocess message cannot be delivered | Durable retry, then escalation. |

## Test Implications

- Manager tests cover trigger handling, queue idempotency, incident lifecycle, policy order, recovery lifecycle, branch decision, subprocess messages, escalation, and raw diagnostic redaction.
- Negative tests prove manager cannot directly mutate runtime state.
- Integration tests prove manager decisions flow through runtime transitions and events.
