# Security Governance And Agent Change Auditing

## Design Intent

Process execution can launch agents, workflows, subprocesses, file operations, recovery actions, and template changes. The architecture needs a governance layer that is explicit, auditable, and generic. Domain-specific risk interpretation belongs to drivers and strategies; enforcement boundaries belong to core/application/runtime policies.

## Model Concepts

Governance concepts:

- `SecurityPlan`: immutable plan section with allowed actors, scopes, approvals, and sensitivity rules.
- `ActorIdentity`: user, manager, runtime, dispatcher, strategy, agent, workflow, or system actor.
- `MutationScope`: allowed paths, artifact stores, template areas, runtime actions, and Git operations.
- `ApprovalPolicy`: actions requiring explicit user or owner approval.
- `AuditEvent`: security-relevant runtime or template event.
- `RestrictedEvidenceRef`: pointer to raw diagnostic or sensitive artifact.
- `AgentChangeAudit`: Git status/diff result compared with allowed mutation scopes.
- `EscalationOwner`: user, role, team, or system queue responsible for a blocked decision.

## Required Controls

- Path authorization for file and Git operations.
- Agent allowed mutation scopes recorded in the instance plan.
- Process manager change audits through Git wrapper.
- Secret scanning boundary before raw diagnostics or artifacts are exposed.
- Sensitivity classification for raw diagnostics, artifacts, events, and projections.
- Approval-required actions for risky recovery, external mutation, credential access, template migration, conflict resolution, and destructive Git operations.
- Escalation owner assignment for incidents that cannot be automatically resolved.
- Audit event retention and redaction policy.

## Agent Change Audit

Agent-backed or tool-backed strategies that can mutate repository files must provide:

- declared allowed paths,
- declared forbidden paths,
- expected change intent,
- run/step/strategy correlation IDs,
- pre-execution Git status baseline,
- post-execution Git status/diff,
- audit comparison result.

Audit outcomes:

- allowed changes only,
- allowed changes plus untracked generated output,
- unauthorized path mutation,
- forbidden deletion,
- suspicious secret-like content,
- dirty baseline prevented audit,
- Git unavailable or command failed.

Unauthorized changes produce a manager incident and can block step completion depending on policy.

## Invariants

- Raw diagnostics and restricted artifacts are never emitted into normal UI projections.
- Every external mutation strategy has an allowed mutation scope.
- Approval-required actions cannot execute without recorded approval.
- Git auditing uses the Git wrapper, not ad hoc shell commands in process code.
- Security decisions are events.
- Redaction happens before user-facing incident projection.
- Sensitivity labels travel with events, artifacts, diagnostics, and projections.

## Failure Behavior

| Failure | Behavior |
| --- | --- |
| Missing security plan | Builder failure. |
| Strategy requests unauthorized scope | Builder failure or manager policy denial before execution. |
| Agent modifies unauthorized file | Manager incident, restricted diff reference, and configured rollback/escalation flow. |
| Secret-like content detected | Restricted incident and approval/escalation requirement. |
| Approval missing | Step waits in approval state. |
| Git audit cannot run | Manager incident; completion policy decides block or escalation. |
| Raw diagnostic lacks sensitivity | Event/artifact write rejected. |

## Boundary Rules

- Core defines generic governance concepts.
- Application and runtime enforce policies.
- Drivers may contribute domain risk facets but not bypass policy.
- Git wrapper performs repository operations.
- UI displays sanitized projections and restricted links only for authorized users.

## Test Implications

- Architecture tests prove runtime/core do not call shell Git directly.
- Security unit tests cover scope matching, approval enforcement, sensitivity propagation, redaction, restricted evidence links, and escalation ownership.
- Integration tests cover Git status/diff audit, unauthorized change detection, dirty baseline handling, and sanitized logs.
- UI tests verify restricted diagnostics are not rendered as raw text.
