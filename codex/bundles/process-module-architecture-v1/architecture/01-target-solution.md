# Target Architecture

## Operating-System Framing

The Process module should be designed like a small operating system:

- Kernel: generic process core, graph rules, identity types, artifact ledger contracts, branch contracts, and transition invariants.
- Scheduler: runtime engine that selects eligible step instances and enforces budgets.
- Dispatcher: execution claim, strategy invocation, result normalization, and handoff back to runtime.
- Process manager: supervisor that interprets results, handles incidents, coordinates subprocess managers, and decides recovery/escalation.
- Device drivers: domain drivers that expose capabilities and strategies without changing the kernel.
- File system: artifact store, artifact ledger, references, ownership, provenance, freshness, and sharing rules.
- Interrupts: typed runtime events and incidents.
- Observability: event stream, observers, snapshots, live cache, and history projections.
- Package manager: template pack loader, migration chain, component override model, and Git-backed versioning.

This framing is useful because it keeps the core small and strict. The runtime knows how to schedule and supervise. Drivers know domain details. UI knows how to display projections. None of those layers should need to know each other's private implementation details.

## Project Boundaries

Recommended target projects:

| Project | Responsibility | Must Not Reference |
| --- | --- | --- |
| `CanDoItAll.Processes.Contracts` | Stable external DTOs and API contracts | EF, Razor, concrete drivers, AgentFramework runtime internals |
| `CanDoItAll.Processes.Abstractions` | Generic interfaces, IDs, capability descriptors, strategy contracts | EF, Razor, domain drivers |
| `CanDoItAll.Processes.Core` | Pure process kernel models and rules | EF, Razor, infrastructure, drivers |
| `CanDoItAll.Processes.Templates` | JSON template schemas, component model, migrations, override merge | UI, runtime execution |
| `CanDoItAll.Processes.Builder` | Definition and instance composition, driver/strategy selection | UI, concrete persistence |
| `CanDoItAll.Processes.Runtime` | Runtime engine, scheduler, dispatcher contracts, manager runtime, event emission | Razor, concrete UI |
| `CanDoItAll.Processes.Persistence` | EF entities, event store, snapshot store, indexes | UI components |
| `CanDoItAll.Processes.Application` | Use cases, app services, API orchestration | Razor components |
| `CanDoItAll.Processes.Drivers.Abstractions` | Driver contracts and driver-contributed capability descriptors | UI, persistence |
| `CanDoItAll.Processes.Drivers.*` | Domain-specific drivers and strategies | UI module |
| `CanDoItAll.Git` | Typed wrapper over Git executable | Process UI |
| `CanDoItAll.Components.Git` | Reusable Git UI components | Process runtime |
| `CanDoItAll.Modules.Processes` | Blazor pages/components and UI presenters | EF entities directly, runtime internals |

The UI module should depend on application services and read models. It must not orchestrate process execution.

## Generic Core Model

The core should define strongly typed identifiers:

```csharp
public readonly record struct ProcessDefinitionId(Guid Value);
public readonly record struct ProcessInstanceId(Guid Value);
public readonly record struct ProcessRunId(Guid Value);
public readonly record struct ProcessStepDefinitionId(Guid Value);
public readonly record struct ProcessStepInstanceId(Guid Value);
public readonly record struct ArtifactDefinitionId(Guid Value);
public readonly record struct ArtifactSlotId(Guid Value);
public readonly record struct ArtifactInstanceId(Guid Value);
public readonly record struct DriverId(string Value);
public readonly record struct StrategyId(string Value);
public readonly record struct BranchDefinitionId(string Value);
```

The core separates:

- `ProcessDefinition`: versioned design-time graph.
- `ProcessInstancePlan`: immutable composition for a specific run.
- `ProcessRuntimeState`: mutable state machine state.
- `ProcessRuntimeEvent`: append-only event.
- `ProcessSnapshot`: projected read model.

The core must not know names like Blazor, .NET, Office, marketing, GitHub, Playwright, Microsoft Agent Framework, or browser proof. Those are driver capabilities and strategy implementations.

## Process Definition

A process definition contains:

- metadata and governance text,
- role definitions or component references,
- artifact definitions or component references,
- step definitions,
- branch/switch definitions,
- subprocess references,
- manager profile reference,
- monitoring profile reference,
- recovery policy reference,
- loop budget policy,
- template source metadata.

Steps define intent, not implementation details:

```csharp
public sealed record ProcessStepDefinition(
    ProcessStepDefinitionId Id,
    ProcessStepKey Key,
    string Title,
    StepExecutionKind ExecutionKind,
    StrategySelector StrategySelector,
    IReadOnlyList<ArtifactInputRequirement> Inputs,
    IReadOnlyList<ArtifactOutputRequirement> Outputs,
    IReadOnlyList<BranchDefinitionRef> Branches,
    IReadOnlyList<StepDependency> Dependencies,
    StepLoopBudget LoopBudget);
```

`StepExecutionKind` remains generic:

- `Manual`
- `Normal`
- `Subprocess`
- `Workflow`
- `Agent`
- `AgentGroup`
- `HandoffFlow`
- `Switch`
- `Approval`
- `End`

Domain detail lives in strategy selectors and driver metadata.

## Process Instance Plan

The builder creates a `ProcessInstancePlan` before execution:

- resolved definition snapshot,
- selected driver stack,
- selected process manager strategy,
- selected error preprocessing strategy,
- selected artifact resolver/recovery strategies,
- step instances with assigned execution strategy IDs,
- subprocess instance plans,
- artifact slots and initial ledger entries,
- branch route table,
- loop budgets,
- monitoring configuration,
- persistence and retention profile,
- correlation IDs.

This plan is persisted. Runtime can then execute deterministically from the plan.

## Runtime Engine

The runtime engine owns:

- loading instance plans,
- applying state transitions,
- enforcing graph invariants,
- calculating eligible steps,
- enforcing loop/recovery/escalation budgets,
- writing runtime state,
- writing runtime events,
- scheduling dispatcher work,
- handling cancellation and terminal run states.

The runtime does not call agents, workflows, browsers, Office APIs, or Git directly. It delegates execution to dispatcher plus strategy.

## Dispatcher

The dispatcher owns:

- selecting ready work from runtime queue,
- safely claiming work with leases,
- loading the assigned step execution strategy,
- invoking that strategy with a typed execution context,
- capturing result envelopes,
- handing results back to runtime,
- renewing or releasing leases.

The dispatcher does not decide domain recovery. It reports normalized results and incidents to the manager/runtime.

## Process Manager

The process manager is a generic supervisor. It can be backed by deterministic rules, an agent, or a hybrid strategy. It owns:

- interpreting step results,
- interpreting subprocess results,
- preprocessing errors,
- deciding automatic recovery versus escalation,
- requesting artifact recovery/resupply,
- coordinating with subprocess managers,
- deciding branch outcomes when configured,
- producing user-facing incidents,
- enforcing recovery and loop limits,
- emitting manager decisions as events.

The manager sees domain concepts only through driver-provided `ManagerContextFacet` objects and strategy outputs.

## Driver Layering

Drivers are selected through capability descriptors:

```csharp
public sealed record ProcessDriverDescriptor(
    DriverId Id,
    string DisplayName,
    DriverLayer Layer,
    IReadOnlyList<CapabilityTag> Capabilities,
    IReadOnlyList<DriverId> Requires,
    IReadOnlyList<DriverId> Extends,
    int Priority);
```

`DriverLayer` is generic:

- `Foundation`
- `Domain`
- `Platform`
- `Framework`
- `SubFramework`
- `Tooling`
- `Organization`
- `Project`

For a Blazor WASM run, the selected stack might be:

- software-development driver as `Domain`,
- dotnet driver as `Platform`,
- blazor driver as `Framework`,
- blazor-wasm driver as `SubFramework`.

The runtime only sees descriptors, capabilities, and strategies. It does not know what Blazor WASM means.

## Driver Responsibilities

A driver may provide:

- execution strategy factories,
- artifact recovery strategies,
- artifact validation strategies,
- manager decision strategies,
- error preprocessing strategies,
- branch definition families,
- branch decision strategies,
- subprocess communication strategies,
- template components,
- migration contributors,
- monitoring facets,
- policy descriptors,
- diagnostics mappers.

Drivers must be replaceable and testable in isolation.

## Strategy Contracts

Minimum strategy interfaces:

```csharp
public interface IProcessStepExecutionStrategy
{
    StrategyId Id { get; }

    ValueTask<StepExecutionResult> ExecuteAsync(
        StepExecutionContext context,
        CancellationToken cancellationToken);
}

public interface IArtifactRecoveryStrategy
{
    StrategyId Id { get; }

    ValueTask<ArtifactRecoveryResult> RecoverAsync(
        ArtifactRecoveryContext context,
        CancellationToken cancellationToken);
}

public interface IProcessManagerDecisionStrategy
{
    StrategyId Id { get; }

    ValueTask<ManagerDecision> DecideAsync(
        ManagerDecisionContext context,
        CancellationToken cancellationToken);
}

public interface IBranchDecisionStrategy
{
    StrategyId Id { get; }

    ValueTask<BranchDecision> DecideAsync(
        BranchDecisionContext context,
        CancellationToken cancellationToken);
}
```

Additional strategies:

- `IErrorPreprocessorStrategy`
- `IRecoveryEscalationStrategy`
- `IArtifactResupplyStrategy`
- `ISubprocessManagerCommunicationStrategy`
- `ILoopProtectionStrategy`
- `ITemplateMergeStrategy`
- `ITemplateMigrationStrategy`
- `IMonitoringProjectionStrategy`

## Step Execution Strategies

The builder assigns one strategy to each step instance:

| Execution Kind | Strategy Responsibility |
| --- | --- |
| `Normal` | Generic work step, usually human/agent/manual strategy based on role binding. |
| `Subprocess` | Starts or observes composed child process instance. |
| `Workflow` | Invokes workflow runtime and maps workflow outputs to artifact slots. |
| `Agent` | Invokes a single agent with governed context and finalizer contract. |
| `AgentGroup` | Coordinates multiple agents through a collaboration strategy. |
| `HandoffFlow` | Executes handoff-capable agent flow such as Microsoft Agent Framework handoffs through an adapter strategy. |
| `Switch` | Requests manager/branch strategy decision and applies route. |
| `Approval` | Waits for approval event or approval strategy result. |

This prevents runtime from guessing execution behavior after the run starts.

## Artifact System

The artifact system uses four separate concepts:

- `ArtifactDefinition`: design-time description from template or process definition.
- `ArtifactSlot`: required or optional artifact position in an instance plan.
- `ArtifactInstance`: actual produced artifact with content location or external reference.
- `ArtifactReference`: scoped reference from a consumer to an artifact instance or slot.

Artifact availability states:

- `Expected`
- `PendingProducer`
- `Available`
- `Recovering`
- `Resupplied`
- `Missing`
- `Rejected`
- `Stale`
- `Superseded`
- `Expired`
- `Inaccessible`

Every artifact records:

- owner process,
- owner step,
- owner role,
- producing execution attempt,
- parent/child source if projected,
- provenance,
- trust status,
- sensitivity,
- retention,
- freshness,
- validation status,
- allowed consumers,
- recovery strategy reference,
- current availability.

Later steps reference artifact slots rather than relying on immediate predecessor assumptions.

## Artifact Recovery

When a consumer lacks an artifact, runtime creates an `ArtifactRecoveryIncident`. The manager receives:

- missing artifact slot,
- consuming step,
- producer candidates,
- available historical artifacts,
- parent/child references,
- current recovery budget,
- driver facets,
- user escalation policy.

The manager selects one of:

- wait for materialization,
- ask producer step manager to resupply,
- rerun producer step,
- run recovery-only strategy,
- import external artifact,
- ask user,
- block/escalate.

All recovery actions emit events and update the artifact ledger.

## Error Handling

Errors are modeled in layers:

- `RuntimeFault`: generic engine/dispatcher/persistence/timeout/cancellation issue.
- `ExecutionFault`: strategy-level failure.
- `DomainDiagnostic`: driver-owned diagnostic payload.
- `ManagerIncident`: user-facing, preprocessed issue.
- `Escalation`: explicit request for higher authority or human intervention.

The manager never dumps raw agent output directly to the user. It preprocesses through `IErrorPreprocessorStrategy` and stores raw details in restricted diagnostic artifacts when needed.

Automatic recovery is allowed only when:

- the run policy permits it,
- the strategy supports it,
- budget remains,
- no required approval is missing,
- idempotency checks pass,
- the same failure fingerprint has not exceeded configured repetition limits.

## Parent And Subprocess Manager Communication

Parent and child managers communicate through typed control messages:

- `SubprocessStarted`
- `SubprocessProgress`
- `SubprocessArtifactAvailable`
- `SubprocessArtifactMissing`
- `SubprocessRecoveryRequested`
- `SubprocessBlocked`
- `SubprocessEscalated`
- `SubprocessCompleted`
- `SubprocessFailed`

The communication strategy defines tone and detail level for the domain, but the message envelope is generic. Parent managers can request artifact resupply or clarification from child managers without directly mutating child state.

## Monitoring And Snapshots

Runtime emits typed events to a durable event stream:

- process created/started/completed/failed/cancelled,
- step ready/started/completed/blocked/failed/skipped,
- strategy selected,
- dispatcher claimed/released,
- artifact expected/available/missing/recovered/resupplied/rejected,
- branch decision requested/selected,
- loop budget consumed/exceeded,
- manager decision,
- recovery attempt,
- escalation opened/updated/resolved,
- subprocess lifecycle event,
- snapshot projection updated.

Runtime event writes must be fast. Observers consume through an outbox or channel. Snapshot builders run asynchronously and write:

- current process snapshot,
- live dashboard snapshot,
- run detail snapshot,
- stage snapshot,
- timeline projection,
- artifact map projection,
- manager incident projection,
- historical metrics projection.

Live mode reads latest current snapshots. History mode reads historical projections filtered by event timestamp. Explicit refresh bypasses cache but still reads projections, not runtime internals.

## Template System

Template source of truth is JSON. Markdown and Mermaid are generated on demand from JSON and may be cached with source hash metadata.

Template components:

- roles,
- artifacts,
- steps,
- branches,
- manager profiles,
- recovery policies,
- monitoring profiles,
- driver selectors,
- strategy selectors.

Every component has:

- stable key,
- schema version,
- content version,
- content hash,
- base component reference if overridden,
- owner,
- compatibility range,
- migration state.

Local overrides are patch objects, not copied full objects unless deliberately detached.

## Template Updates And Conflicts

When a global component changes, usages are evaluated with a three-way merge:

- old global base,
- new global base,
- local override.

Outcomes:

- clean update,
- clean update with local override preserved,
- conflict,
- detached local copy,
- blocked by schema migration.

The UI must show changed fields, local overrides, conflicts, and resolution choices.

## Template Migrations

Template migrations are deterministic version-to-version transformations.

Recommendation:

- Run a repository migration command for all templates when the app detects schema drift.
- Support chained lazy migration for emergency reads, but do not rely on lazy migration as the only path.
- Never skip intermediate migrations.
- Parallelize migration across template files where dependency ordering permits.
- Create a Git branch or commit before migration.
- Record migration manifest with source hashes, target hashes, and results.

Nullable policy:

- Omit optional JSON properties when default behavior is intended.
- Use nullable DTO properties only for true tri-state semantics.
- Use explicit default resolvers during load and migration.
- Do not add empty fields to every template only to satisfy DTO shape.

## Git Wrapper

Create `CanDoItAll.Git` as a typed wrapper over the Git executable. Do not implement Git algorithms.

Core operations:

- repository discovery,
- status using porcelain v2,
- diff name/status,
- text diff,
- stage/unstage,
- commit,
- branch,
- checkout,
- merge,
- conflict detection,
- conflict file read/write,
- log,
- show,
- tag,
- worktree,
- path authorization checks,
- file hash and content snapshot.

Process usage:

- version templates and instructions,
- create migration branches,
- track process-run changes,
- let manager check unauthorized agent modifications,
- attach Git evidence to process artifacts,
- support conflict resolution UI.

## Git UI Components

Create reusable components:

- `GitStatusPanel`
- `GitDiffViewer`
- `GitCommitPanel`
- `GitBranchSelector`
- `GitMergePanel`
- `GitConflictList`
- `GitConflictResolutionEditor`
- `GitChangeGuardBanner`
- `GitTemplateUpdateReview`

These components are generic. Process UI composes them for templates and process-run change audits.

## Branch And Switch Design

A branch definition contains:

- branch key,
- domain-neutral category,
- required input artifact slots,
- optional manager prompt strategy,
- outcome options,
- route targets,
- backward-route flag,
- loop budget,
- escalation policy,
- display metadata.

Domain drivers can contribute branch families. Users can:

- select a generic branch,
- select a domain-specific branch,
- override labels/descriptions/routes/budgets,
- detach from global branch definition when needed.

Branch decisions are recorded as manager decisions with:

- input artifacts used,
- strategy used,
- selected outcome,
- confidence/quality notes,
- route target,
- budget impact,
- user-facing explanation.

## Loop Protection

Loop protection is mandatory for any route to an earlier step.

Budgets:

- per route edge,
- per branch definition,
- per step,
- per recovery incident,
- per process run,
- per subprocess subtree.

Runtime computes a path fingerprint from:

- source step,
- selected branch outcome,
- target step,
- incident fingerprint,
- artifact slot fingerprints,
- domain strategy ID.

When the same fingerprint exceeds the configured budget, runtime raises escalation and stops automatic routing unless manager policy explicitly allows one higher-level recovery attempt.

## UI Projections

UI reads only projection models:

- process definition tree,
- process canvas definition projection,
- process canvas runtime projection,
- live process dashboard,
- run detail view,
- artifact ledger view,
- manager incident inbox,
- template catalog,
- template diff/conflict review,
- Git status/diff/commit views.

The UI must not infer runtime truth from raw events, EF entities, or driver internals.

