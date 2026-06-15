# Why The Current Runtime And Dispatcher Are Insufficient

## 1. Runtime Semantics Live In The UI/Application Module

The main runtime and dispatcher behavior lives under `src/CanDoItAll.Modules.Processes`. That module also contains Razor components, template library services, persistence entities, launch planning, canvas services, observation services, and user-facing presenters.

This violates the target boundary. The generic runtime must be reusable without Blazor, current UI services, EF entity classes, or concrete agent/workspace modules.

## 2. The Dispatcher Is A Large Partial Orchestrator

`ProcessRunAutomationDispatchService` is spread across many partial files and directly handles route selection, agent invocation, workflow invocation, artifact projection, validation, retry, browser proof, provider fallback, subprocess handling, finalization, and prompt construction.

This creates several risks:

- New execution kinds require editing the central dispatcher.
- Domain-specific behavior leaks into generic orchestration.
- Recovery behavior is difficult to reason about because it is embedded in execution and finalization paths.
- Tests become static and brittle because behavior is distributed across partial files rather than explicit contracts.
- Runtime state transitions are coupled to current EF persistence and outbox implementation.

## 3. Drivers Are Too Narrow

Current drivers verify evidence and produce diagnostics. They do not provide:

- layered driver composition,
- capability scoring,
- strategy factories,
- manager policy fragments,
- branch definitions,
- artifact recovery handlers,
- resupply policies,
- template component packs,
- runtime execution adapters.

The architecture requires drivers to be a domain extension mechanism, not only a read-only verification lane.

## 4. Process Instance Composition Is Not First-Class

The current start path creates runs, assignments, step runs, work briefs, outbox records, and project-structure sync inside `ProcessesService.StartRunAsync`. It does not produce a first-class immutable instance composition that captures selected drivers, strategies, branch behavior, manager behavior, recovery policy, subprocess plans, and monitoring configuration.

Without that composition artifact, runtime behavior becomes an afterthought and varies based on dispatcher code paths instead of a declared run plan.

## 5. Artifacts Are Recorded But Not Governed As A Full Artifact System

`ProcessArtifactExpectation`, `ProcessStepArtifactInputDefinition`, and `ProcessArtifactRecord` are useful, but the model does not yet provide a complete artifact operating system:

- artifact slots,
- availability states,
- ownership and sharing policy,
- freshness,
- parent/child references,
- branch/manager artifact inputs,
- recovery/resupply requests,
- stale or superseded artifact handling,
- reference safety across process boundaries.

The target runtime needs an artifact ledger and resolver, not only per-step artifact records.

## 6. Branching Depends Too Much On Text

The current branch model has outcomes and dependencies, but domain semantics are inferred from keys/titles/descriptions. That is fragile. Users will not reliably author correct branch text.

The target branch system needs typed branch definitions, branch outcome IDs, manager decision strategy, branch input contracts, domain-provided option sets, and loop budgets.

## 7. Monitoring Is Projection-First Rather Than Event-First

The current observation service builds snapshots by querying current services and caching the result. This is useful for UI responsiveness, but it means runtime observability is not a durable event stream first.

The target design must emit typed runtime events, asynchronously build snapshots, persist historical projections, and let live/history views consume projections without touching runtime execution paths.

## 8. Recovery Is Too Agent-Specific

The recovery model contains useful details, but much of it is agent-centric and dispatcher-specific. The requested architecture needs generic recovery and escalation mechanisms that can also handle Office file locks, workflow failures, process-manager disputes, subprocess manager communication, external tool failures, and future domain blockers.

## 9. Template Versioning Is Incomplete

Templates are file-based JSON plus sidecar Markdown/Mermaid/projection files. There is no complete component versioning, override merge, schema migration chain, or conflict-resolution model. A large template set cannot be safely evolved without this.

## 10. The Existing System Cannot Be Fixed By Wrapping It

The current code contains useful parts, but the central failure is architectural. Wrapping a new API around the existing dispatcher would preserve the coupling and make the next rewrite harder. The correct approach is to copy the old implementation as reference material, remove the old Process module on the rewrite branch, and rebuild from clean project boundaries with tests at each layer.

