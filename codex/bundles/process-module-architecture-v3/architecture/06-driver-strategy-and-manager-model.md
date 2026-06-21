# Driver Strategy And Manager Model

## Design Intent

Drivers are domain extension providers. Strategies are executable behavior selected into an instance plan. The manager is a generic supervisor that uses strategy outputs, policies, events, artifacts, and driver-provided facets to decide recovery, escalation, branch routing, and subprocess communication.

The core runtime must not gain domain branches whenever a new work type appears. New work types are added by drivers and strategies.

## Model Concepts

Driver package concepts:

- `ProcessDriverDescriptor`: identity, version, supported runtime schema, dependencies, conflicts, capability tags, and provided facets.
- `ProcessDriverPackage`: descriptor plus strategy factories, branch definitions, artifact validators, recovery handlers, manager policy fragments, template fragments, and projection facets.
- `DriverCatalog`: registry of available driver packages.
- `DriverStackFactory`: resolves ordered driver stack for a run-specific capability request.
- `CapabilityRequest`: requested capability tags and constraints from definition/run context.
- `CapabilityMatchResult`: score, selected drivers, missing capabilities, conflicts, and diagnostics.
- `DriverConflict`: incompatible drivers, duplicate exclusive capabilities, schema mismatch, or policy mismatch.
- `DriverLayer`: broad base, platform, framework, scenario, and local override ordering.

Strategy families:

| Strategy | Bound by | Executed by | Mutates runtime? |
| --- | --- | --- | --- |
| Step execution | Builder | Dispatcher | No, returns result envelope. |
| Branch decision | Builder/manager | Manager | No, returns decision. |
| Manager decision | Builder | Manager runtime | No direct mutation; emits decision. |
| Error preprocessing | Builder | Manager | No, returns incident content. |
| Artifact recovery | Manager | Dispatcher or manager-controlled executor | No direct mutation; returns recovery result. |
| Artifact resupply | Manager | Dispatcher or manager-controlled executor | No direct mutation; returns resupply result. |
| Artifact validation | Builder | Runtime/manager/projector | No direct mutation. |
| Subprocess communication | Builder | Manager runtime | Emits control messages. |
| Loop protection | Builder | Runtime | Runtime applies budget transitions. |
| Template merge/migration | Template layer | Template services | Mutates template files through Git workflow only. |

## Driver Stack Rules

- Drivers can extend other drivers.
- Drivers can require other drivers by ID and compatible version range.
- A specific sub-driver may override a broad driver strategy only through declared capability precedence.
- Domain-specific capability names remain opaque outside the driver layer.
- Driver diagnostics are exposed as domain facets for managers and projections, not as core runtime concepts.
- Conflicts are reported during builder composition; runtime does not recover by picking a different stack.
- Strategy binding snapshots include driver ID, strategy ID, strategy version, factory version, compatibility range, and binding inputs hash.

## Manager Types

| Manager type | Use | Constraint |
| --- | --- | --- |
| Deterministic manager | Straightforward processes and strict compliance flows. | Decisions are rule-based and auditable. |
| Agent-backed manager | Ambiguous or judgment-heavy process supervision. | Agent output is preprocessed and policy-checked before runtime transitions. |
| Hybrid manager | Common target: deterministic policy with agent-assisted analysis. | Deterministic policy owns permissions, budgets, and escalation. |

Manager inputs:

- instance plan,
- runtime state snapshot,
- recent runtime events,
- manager incident context,
- artifact slots and instances,
- driver-provided facets,
- policies and budgets,
- subprocess manager messages,
- user responses and approvals.

Manager outputs:

- decision event,
- user-facing incident,
- recovery request,
- artifact resupply request,
- branch decision,
- subprocess control message,
- escalation request,
- approval request.

## Result Envelopes

All strategies return normalized envelopes:

```csharp
public sealed record StrategyResultEnvelope(
    StrategyId StrategyId,
    string StrategyVersion,
    Guid IdempotencyKey,
    StrategyOutcome Outcome,
    IReadOnlyList<ProducedArtifactRef> ProducedArtifacts,
    IReadOnlyList<RequestedArtifactRef> RequestedArtifacts,
    IReadOnlyList<StrategyDiagnosticRef> Diagnostics,
    IReadOnlyList<ManagerSignal> ManagerSignals,
    string ResultHash);
```

Raw diagnostics are stored as restricted evidence references. The envelope contains user-safe summaries, classifications, and references.

## Invariants

- Runtime can execute only strategies bound in the instance plan.
- Missing strategy implementation at runtime is a hard incident, not a fallback opportunity.
- A strategy cannot mutate runtime state; it returns an envelope.
- Driver-specific diagnostics cannot become core state names.
- A manager decision that changes route, recovery, escalation, or user communication must be recorded as an event.
- Automatic manager recovery is allowed only when policy permits it, budget remains, required approval exists or is not required, the action is idempotent or explicitly safe to repeat, and the failure fingerprint is under budget.

## Failure Behavior

| Failure | Behavior |
| --- | --- |
| Driver dependency missing | Builder fails with dependency diagnostic. |
| Driver conflict | Builder fails with conflict list and affected capabilities. |
| Strategy binding missing | Builder fails; runtime cannot start. |
| Strategy implementation unavailable after deployment | Runtime emits restricted infrastructure incident and escalates to operator. |
| Manager cannot classify result | Manager emits user-safe uncertainty incident and requests escalation or configured fallback strategy only if explicitly bound. |
| Agent-backed manager produces unsafe action | Policy rejects action, records manager decision denial, and escalates if no allowed alternative remains. |

## Boundary Rules

- `Processes.Drivers.Abstractions` defines driver and strategy contracts but not concrete domain behavior.
- Concrete driver projects may reference domain libraries needed for their work, but not UI modules.
- `Processes.Runtime` depends on abstractions and strategy interfaces; concrete strategy resolution is through a catalog created by application composition.
- Manager policy is generic; domain-specific analysis is returned as driver facets or strategy outputs.
- Operational manager queue/control-loop behavior is detailed in `architecture/14-manager-runtime-and-control-loop.md`.
- Driver package contracts live in `CanDoItAll.Processes.Drivers.Abstractions`, which must be implemented before Builder.
- Execution integrations are adapter strategies described in `architecture/16-execution-adapters-and-integration-boundaries.md`.

## Test Implications

- Driver catalog tests cover dependency ordering, conflict detection, precedence, capability scoring, and version compatibility.
- Strategy binding tests prove the builder persists selected strategy IDs and versions.
- Runtime tests simulate missing strategy implementations and verify escalation.
- Manager tests cover deterministic decisions, agent-backed preprocessing, unsafe action rejection, recovery budget consumption, subprocess messages, and escalation.
- Domain leakage tests scan generic projects for driver-specific vocabulary.
