# Core Model And Invariants

## Design Intent

The core is the Process kernel. It defines domain-neutral identities, graph rules, artifact abstractions, branch contracts, state-transition invariants, and event contracts. It must be small enough to reason about and strict enough to prevent the old dispatcher shape from returning under new names.

The core does not execute work, call agents, open files, invoke workflows, run Git, render UI, or know domain capability names. It defines the rules every higher layer must obey.

## Model Concepts

| Term | Meaning | Must not contain |
| --- | --- | --- |
| Process Definition | Versioned design-time process graph. | Runtime status, EF entity details, domain tool names. |
| Template Component | Reusable JSON component used by process definitions. | Runtime execution state. |
| Definition Snapshot | Published immutable definition materialized from template source. | Local working-tree edit state. |
| Process Instance Plan | Immutable compiled run plan. | Mutable progress. |
| Runtime State | Mutable process, step, artifact, and incident state. | UI projection formatting. |
| Runtime Event | Append-only record of a state change or decision. | Unversioned raw object dumps. |
| Snapshot/Projection | UI/read-model materialized from events. | Authoritative runtime mutation logic. |
| Driver | Domain extension provider. | Generic runtime state transitions. |
| Strategy | Executable behavior selected at build time. | Direct runtime state mutation. |
| Manager | Supervisor and decision layer. | Hardcoded domain-specific code in core. |

Core identities are strongly typed value objects, not loose strings:

```csharp
public readonly record struct ProcessDefinitionId(Guid Value);
public readonly record struct ProcessDefinitionVersionId(Guid Value);
public readonly record struct ProcessInstancePlanId(Guid Value);
public readonly record struct ProcessRunId(Guid Value);
public readonly record struct ProcessStepDefinitionId(Guid Value);
public readonly record struct ProcessStepInstanceId(Guid Value);
public readonly record struct ArtifactDefinitionId(Guid Value);
public readonly record struct ArtifactSlotId(Guid Value);
public readonly record struct ArtifactInstanceId(Guid Value);
public readonly record struct RuntimeEventId(Guid Value);
public readonly record struct DriverId(string Value);
public readonly record struct StrategyId(string Value);
public readonly record struct CapabilityTag(string Value);
```

`CapabilityTag` is opaque. The core may compare it for equality and persist it as metadata, but it must not branch on specific tag values.

## Plane Separation

| Plane | Example types/files | Mutability | Persistence | Owner |
| --- | --- | --- | --- | --- |
| Template source | `process.json`, component JSON, override patch JSON | Versioned files | Git plus DB index | Templates layer |
| Definition snapshot | `ProcessDefinitionSnapshot` | Immutable after publish | DB/file index | Templates/Application |
| Instance plan | `ProcessInstancePlan` | Immutable after creation | DB JSON or structured tables | Builder |
| Runtime state | `ProcessRuntimeState`, `StepRuntimeState` | Mutable by runtime only | DB state tables | Runtime |
| Runtime events | `ProcessRuntimeEventEnvelope` | Append-only | Event store/outbox | Runtime |
| Artifact ledger | `ArtifactLedgerEvent` | Append-only | Event store or artifact ledger table | Runtime/artifact subsystem |
| Projection | `LiveProcessSnapshot`, `RunDetailProjection` | Rebuilt/overwritten | Projection tables/cache | Projectors |
| UI model | Razor/component view models | Request scoped | Memory/UI | UI module |

## Invariants

- Definitions are acyclic unless an edge is explicitly marked as a backward branch edge.
- Every backward branch edge has a loop budget and path fingerprint rule.
- Published definition snapshots are immutable.
- Instance plans are immutable once persisted.
- Runtime state changes are produced only by validated transitions.
- A step may become ready only when required predecessor dependencies and required artifact slots are satisfied or explicitly waived by policy.
- Every executable step has a strategy binding before runtime starts.
- Runtime events contain event ID, root run ID, run ID, correlation ID, optional causation ID, actor, schema version, sensitivity, and UTC timestamp.
- A manager decision is recorded for every manager-selected branch, recovery action, escalation, user incident, or subprocess control message.
- A recovery attempt has a budget record, policy decision, idempotency classification, and failure fingerprint.
- A child process plan has a parent step reference, root run reference, depth value, and depth budget.
- A subprocess cannot directly mutate parent runtime state.
- Raw diagnostics are restricted evidence, not normal UI messages.
- Snapshot/projection rows cannot be used as authoritative runtime state.

## Failure Behavior

Core validation returns explicit failures. It does not use silent fallback behavior.

| Failure | Output |
| --- | --- |
| Domain term detected in core/runtime contract | Architecture test failure and build gate failure. |
| Missing required strategy binding | Builder failure; runtime cannot start. |
| Invalid transition | Runtime transition rejection event and manager incident if user action is needed. |
| Backward edge without budget | Definition publication failure. |
| Artifact reference crosses process boundary without policy | Builder failure or runtime incident, depending on when detected. |
| Raw diagnostic lacks sensitivity classification | Event write rejected. |

## Boundary Rules

- `CanDoItAll.Processes.Core` may reference only contracts/abstractions and base runtime libraries.
- Core must not reference EF, Razor, Blazor components, concrete driver projects, application services, Git implementation, AgentFramework runtime internals, or storage infrastructure.
- Core may define generic `ExecutionKind`, `CapabilityTag`, `StrategyId`, and `DriverId`.
- Core must not contain product/domain-specific branch outcome names, tool names, file path rules, provider names, or UI route names.
- Core rule types must be deterministic and easy to unit test.

## Test Implications

- Architecture tests prove forbidden references are absent.
- Vocabulary leak tests scan core/runtime contracts for banned domain terms except in explicit example documents.
- Core unit tests cover graph validation, branch edge budgets, artifact reference scope, state-transition tables, loop fingerprints, and event envelope validation.
- Mutation tests or red-team fixtures should prove invalid transitions and missing strategy bindings fail loudly.
