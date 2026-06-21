# Artifact Error Recovery And Subprocess Model

## Design Intent

Artifacts are the Process file system. They are not loose output records attached to a completed step. They are governed objects with ownership, scope, references, lineage, validation, freshness, access policy, recovery, and resupply behavior.

Errors and recovery are manager-driven. Raw faults are preserved as restricted evidence, preprocessed into user-actionable incidents, and resolved through configured strategies with budgets.

Subprocesses are child processes with their own plans, managers, runtime states, events, artifacts, and projections. They communicate with parents through durable control messages and artifact projections, never by directly mutating parent state.

## Artifact Model Concepts

| Concept | Meaning |
| --- | --- |
| `ArtifactDefinition` | Design-time artifact contract from template/definition source. |
| `ArtifactSlot` | Runtime requirement position created in an instance plan. |
| `ArtifactInstance` | Produced object, external reference, or generated content attached to a slot. |
| `ArtifactReference` | Scoped reference from a consumer to a slot or instance. |
| `ArtifactLedgerEvent` | Append-only artifact state change. |
| `ArtifactValidationRecord` | Validation result and validator strategy metadata. |
| `ArtifactAccessPolicy` | Allowed producers/consumers, sensitivity, retention, and cross-process permissions. |
| `ArtifactLineage` | Parent, child, recovery, derived-from, or supersedes relationship. |
| `ArtifactFreshnessPolicy` | Rules for staleness, expiration, and required revalidation. |

Availability states:

```text
Planned -> Required -> PendingProducer -> Available -> Validated
Available -> Stale -> RevalidationRequired -> Validated
Required -> Missing -> RecoveryRequested -> PendingProducer
Available -> Superseded
Available -> Revoked
```

Artifact requirement kinds:

- required,
- optional,
- conditional,
- manager-required,
- branch-required,
- subprocess-import,
- subprocess-export,
- recovery-produced.

## Artifact Reference Rules

- Later steps reference artifact slots, not implicit previous-step output.
- A slot can allow one instance, many instances, or replacement with lineage.
- Cross-process references require a projection rule and access policy.
- A parent may expose selected artifacts to child slots through import projections.
- A child may publish selected artifacts back through export projections.
- Managers and branch strategies can require artifact slots as decision inputs.
- Raw diagnostics and sensitive artifacts require restricted references.
- An artifact can satisfy a slot only if kind, scope, trust, sensitivity, freshness, and validation policy match.

## Error Model Concepts

Fault layers:

- runtime fault,
- persistence fault,
- dispatcher fault,
- strategy fault,
- domain diagnostic,
- policy denial,
- missing artifact,
- blocked external resource,
- manager incident,
- escalation.

Manager incident fields:

- incident ID,
- source event ID,
- affected run/step/artifact IDs,
- raw diagnostic references,
- user-safe title and summary,
- classification,
- severity,
- available actions,
- automatic recovery eligibility,
- budget state,
- escalation owner,
- sensitivity.

## Recovery And Resupply Rules

Automatic recovery is allowed only when:

- policy permits the incident class,
- recovery budget remains,
- loop fingerprint is under budget,
- required approval exists or is not required,
- selected strategy declares idempotency or safe repeat behavior,
- artifact access policy permits the producer/consumer,
- manager records the decision before execution.

Recovery outputs:

- recovered artifact instance,
- resupply request to a producer step, child manager, parent manager, agent, workflow, or driver,
- retry request with changed context,
- waiver request,
- escalation request,
- terminal failure.

## Parent/Subprocess Manager Communication

Communication uses durable messages:

```text
ParentToChildControlMessage
ChildToParentControlMessage
ArtifactProjectionRequest
ArtifactProjectionAccepted
ArtifactProjectionRejected
SubprocessIncidentRaised
SubprocessEscalationRaised
SubprocessCompletionSummary
```

Messages contain run IDs, step IDs, correlation IDs, causation IDs, schema version, sensitivity, requested action, and artifact references. Domain detail is payload/facet data interpreted by a bound subprocess communication strategy.

## Invariants

- Completed step outputs remain available through artifact ledger and event history until retention policy expires.
- Artifact deletion or revocation is a ledger event, not silent removal.
- Missing artifact recovery is a manager decision, not a dispatcher guess.
- A recovery attempt consumes budget before executing the recovery strategy.
- Subprocess managers cannot directly write parent incidents, artifacts, or runtime state.
- Parent and child artifact projection requires explicit access and projection policy.
- Raw diagnostics are never displayed directly as normal UI text.

## Failure Behavior

| Failure | Behavior |
| --- | --- |
| Required artifact missing | Runtime blocks consumer step and manager receives missing-artifact incident. |
| Artifact stale | Runtime requests revalidation or manager decision depending on policy. |
| Artifact access denied | Policy denial incident with restricted diagnostic reference. |
| Recovery repeats without new evidence | Loop fingerprint budget triggers escalation. |
| Child manager rejects artifact request | Parent manager receives control message and selects alternate recovery/escalation. |
| Raw diagnostic too large or sensitive | Store restricted evidence reference and emit user-safe incident summary. |

## Boundary Rules

- Artifact definitions and slots are core concepts.
- Artifact storage backends and external references belong to persistence/infrastructure.
- Artifact recovery behavior belongs to strategies and drivers.
- Manager incidents are generic; domain-specific remediation text is a driver/strategy facet.
- Subprocess communication protocol is generic; domain language is strategy payload.
- Artifact ledger store ports and persistence implementation live behind the model described in `architecture/12-runtime-persistence-event-store-and-outbox.md`.
- Recovery, incident, and manager queue behavior is operationalized in `architecture/14-manager-runtime-and-control-loop.md`.
- Branch/loop budget behavior is defined in `architecture/13-branch-switch-and-loop-contract.md`.

## Test Implications

- Artifact tests cover slot satisfaction, optional/required/conditional inputs, sharing, scope, parent/child projection, freshness, validation, sensitivity, retention, lineage, revocation, and recovery.
- Manager tests cover incident preprocessing, automatic recovery eligibility, resupply requests, budget enforcement, escalation, and raw diagnostic restrictions.
- Subprocess tests cover recursive plan refs, depth budget, cycle rejection, control messages, artifact import/export, cancellation propagation, and escalation propagation.
