# Agent orchestration readiness

## Verdict

The current module is **not ready** to govern a future agent module safely.

The foundation is meaningful:
- role-first process definitions exist,
- runtime steps exist,
- work briefs exist,
- journals and decision rows exist,
- project context exists,
- a future executor seam exists.

But the control-plane requirements for agents are still largely absent or incomplete:
- hard guardrails beyond prompts,
- explicit handoff/baton records,
- approval/escalation routing,
- external executor correlation,
- project-structure propagation of interventions,
- evaluation records,
- rights narrowing,
- concurrency protection.

## Heuristic readiness matrix

| Capability | Current state | Status | Score (0-1) | Risk if deferred |
| --- | --- | --- | --- | --- |
| Canonical execution graph | Single dependency field plus sequence-based activation. | Weak | 0.3 | Agents may execute the wrong next step or bypass intended governance. |
| Baton handoffs and governed routing | Work briefs exist, but no baton or triage records. | Missing-major | 0.1 | Invisible agent-to-agent or human-to-agent routing paths will become shadow workflow. |
| Approval and escalation targeting | WaitingApproval and Blocked statuses exist without explicit route entities. | Weak | 0.25 | Approval semantics stay implicit and operationally unsafe. |
| Hard guardrails beyond prompts | No process-side guardrail service exists; executor seam is noop. | Missing | 0.05 | Unsafe autonomy and inconsistent policy enforcement. |
| Evaluation and scoring | No explicit evaluation record model exists. | Missing | 0.1 | No trustworthy way to compare or control agent quality. |
| External executor correlations | No persisted correlation entity exists. | Stub | 0.05 | Loss of auditability and fragmented observability once external executors are introduced. |
| Project-structure propagation of interventions | Only definition and run nodes are projected. | Missing-major | 0.05 | Escalations stay trapped inside the process view and do not integrate with project control. |
| Identity and template convergence | Project-party binding exists, but role templates are local static definitions. | Mixed | 0.25 | Shadow registries and conflicting source-of-truth boundaries. |
| Journal and replay | Journal rows exist but are not exposed as a first-class operational surface. | Partial | 0.3 | Investigation and supervision will be slower and less reliable. |
| Concurrency safety | No row-version token; same-state transitions are allowed. | Missing | 0.05 | Double completion, conflicting claims, or stale overwrites. |
| Segmented telemetry and bottleneck insight | Basic averages and counts exist. | Weak | 0.25 | Operational decisions will be made on incomplete metrics. |
| Rights and permission narrowing | No structured rights model exists. | Missing | 0.05 | Over-privileged agents and weak human control points. |
| Human-in-the-loop override model | Runtime can be blocked/refused, but override semantics are not modeled. | Weak | 0.2 | Out-of-band overrides will become invisible operational debt. |
| Outcome and customer feedback linkage | No feedback entity exists. | Missing | 0.05 | The system may optimize activity while missing customer harm or weak value realization. |

**Heuristic readiness score:** **15%**

## What must exist before agents are allowed to operate through this module

### 1. Hard runtime governance
Agents must not rely only on prompt phrasing. The module needs code-level checks for:
- who can execute the step,
- what permission scope they receive,
- whether external calls require approval,
- whether the step permits autonomous execution at all.

### 2. Explicit baton semantics
Every handoff between person and person, person and agent, or agent and agent must be persisted with:
- source role,
- target role,
- brief snapshot,
- reason,
- approval/override context,
- completion state.

### 3. Explicit escalation targeting
A blocked or approval-waiting step must be able to target:
- a named human party,
- a supervisory role,
- or a project-structure decision/work item projection.

### 4. Durable correlation back to canonical process context
Future external executor runs need correlation records for:
- runtime session ids,
- log ids,
- approval ids,
- metric ids,
- evaluation ids.

### 5. Human intervention propagation into project structure
This is the most direct response to your operating-model concern.

Recommended projection policy:
- create a **Decision** node when a process step enters an approval or explicit decision-needed state;
- create a **WorkItem** node when a blocked/refused/failed step needs human action;
- attach it under the process run node;
- bind it back to the process run and step ids;
- keep it projection-only;
- close or update it automatically when the underlying process runtime state changes.

### 6. Evaluation and scoring
If the goal is to manage agents effectively, the module also needs:
- structured evaluation result records,
- per-step quality signals,
- refusal and safe-stop scoring,
- exception and rework outcome scoring,
- customer/outcome linkage.

## Minimum safe target for the next milestone

Do not call the module “agent-ready” until the following are all green:
- canonical transitions,
- baton handoffs,
- approval/escalation policies,
- external executor correlations,
- permission narrowing,
- intervention projection into project structure,
- concurrency protection,
- journal/replay access,
- regression coverage for the above.
