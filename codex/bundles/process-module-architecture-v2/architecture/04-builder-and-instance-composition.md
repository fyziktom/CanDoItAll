# Builder And Instance Composition

## Design Intent

The builder is a compiler. It transforms template source, definition snapshots, run context, drivers, strategies, policies, and subprocess references into an immutable `ProcessInstancePlan`. Runtime executes the plan; it does not rediscover execution semantics.

This is the main correction to the current system. Today, `ProcessesService.StartRunAsync` creates runtime records directly and the dispatcher discovers behavior through service code. In the target architecture, composition is explicit, validated, persisted, and hashable before execution starts.

## Model Concepts

Conceptual architecture shape:

```csharp
public sealed record ProcessInstancePlan(
    ProcessInstancePlanHeader Header,
    ResolvedProcessDefinitionSnapshot Definition,
    DriverStackSnapshot DriverStack,
    StrategyBindingSet Strategies,
    IReadOnlyList<StepInstancePlan> Steps,
    ArtifactPlan ArtifactPlan,
    BranchRouteTable Branches,
    IReadOnlyList<SubprocessInstancePlanRef> Subprocesses,
    ManagerPlan Manager,
    BudgetPlan Budgets,
    MonitoringPlan Monitoring,
    SecurityPlan Security,
    string PlanHash);
```

Plan-owned data:

- resolved definition snapshot ID and content hash,
- selected driver stack and driver package versions,
- selected strategy IDs and binding metadata,
- step instance plan IDs,
- subprocess instance plan references,
- artifact slots, initial availability state, and initial ledger entries,
- branch route table and backward route flags,
- manager profile, manager strategy, and communication strategy,
- recovery, retry, loop, escalation, and cancellation budgets,
- monitoring projection configuration,
- security/governance plan,
- plan hash and schema version.

Runtime-resolved data:

- dispatcher lease owner,
- live runtime state,
- external execution run IDs,
- transient strategy execution diagnostics,
- projection freshness.

## Builder Pipeline

1. Parse source request.
2. Resolve template pack and process definition.
3. Run schema migrations.
4. Resolve global components.
5. Apply local overrides.
6. Detect unresolved merge conflicts.
7. Validate graph and branch routes.
8. Validate artifact inputs, outputs, and reference graph.
9. Resolve subprocess definitions recursively.
10. Resolve run context and requested capabilities.
11. Select driver stack.
12. Resolve strategy factories.
13. Bind step execution strategies.
14. Bind manager, branch, error, recovery, artifact validation, and subprocess communication strategies.
15. Build artifact slots and initial ledger entries.
16. Build budget policies and loop guards.
17. Build monitoring plan.
18. Build security/governance plan.
19. Compute plan hash.
20. Persist plan and initial runtime state transactionally.

## Failure Behavior

| Stage | Failure output |
| --- | --- |
| Parse source request | Build diagnostic with source path, schema version, and parse path. |
| Resolve template pack | Missing component diagnostic with component key and requested version. |
| Run migrations | Migration chain failure with from/to schema versions and failed migration ID. |
| Apply local overrides | Conflict record with base, global, local, and target JSON pointer. |
| Validate graph | Definition validation failure with node/edge IDs. |
| Validate artifacts | Artifact contract failure with producer, consumer, slot, optionality, and policy. |
| Resolve subprocesses | Cycle/depth/compatibility failure with parent path. |
| Select driver stack | Capability match failure or conflict list. |
| Bind strategies | Missing strategy binding failure; no runtime fallback is allowed. |
| Build budgets | Policy validation failure for missing loop or recovery budget. |
| Persist plan | Transaction failure; no partial runtime start is allowed. |

## Subprocess Composition

A subprocess step is compiled recursively through the same builder pipeline. The parent plan contains a `SubprocessInstancePlanRef`, not only a definition ID.

Required child plan fields:

- parent instance plan ID,
- parent step instance ID,
- root process instance plan ID,
- hierarchy depth,
- depth budget,
- parent-to-child artifact projection rules,
- child-to-parent artifact publication rules,
- parent/child manager communication strategy binding,
- cancellation propagation policy,
- escalation propagation policy.

Subprocess cycles are detected on definition identity plus resolved version. The builder must reject cyclic plans unless a future explicit recursion feature is designed with a hard budget and termination proof.

## Invariants

- The runtime cannot start a run without a persisted plan.
- A plan hash changes when any selected driver, strategy, definition snapshot, artifact contract, branch route, budget, or security policy changes.
- Every step has exactly one execution strategy binding, including subprocess, workflow, agent, agent-group, handoff, switch, approval, and manual steps.
- Every manager responsibility has a strategy binding or an explicit deterministic built-in policy.
- A child plan cannot reference parent mutable runtime state directly.
- Builder diagnostics are user-actionable enough for template authors and architects.

## Boundary Rules

- Builder depends on templates, core, abstractions, driver abstractions, and application-level catalogs.
- Builder may use persistence through an application transaction boundary, not through UI services.
- Builder may inspect driver descriptors and packages, but it must not execute driver strategies.
- Builder persists selected IDs and compatibility metadata; runtime must not reselect a different strategy for the same step.
- Builder does not render UI and does not generate UI projections.

## Test Implications

- Builder tests cover global component resolution, local overrides, conflict records, graph validation, artifact graph validation, subprocess recursion, depth/cycle checks, driver stack selection, strategy binding, budget creation, monitoring plan creation, and plan hash stability.
- Negative tests prove missing strategies, conflicting drivers, unresolved overrides, and backward branches without budgets fail before runtime.
- Golden-plan tests compare persisted plan snapshots for stable definitions.
