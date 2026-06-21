# Project By Project Rebuild Plan

## Design Intent

The rewrite proceeds from stable contracts to UI. Each project has a dependency gate, test gate, and stop condition. Future implementation must not add behavior to a higher layer to compensate for missing lower-layer contracts.

## Rebuild Matrix

| Order | Project | Depends on | Main deliverable | Required tests | Stop condition |
| --- | --- | --- | --- | --- | --- |
| 1 | `CanDoItAll.Processes.Contracts` | none | External DTOs, API contracts, version markers. | Serialization, compatibility, no infrastructure references. | Any runtime behavior appears in contracts. |
| 2 | `CanDoItAll.Processes.Abstractions` | Contracts | IDs, capability tags, driver/strategy interfaces, manager interfaces. | Contract shape, nullability, domain leakage scan. | Concrete driver or UI reference appears. |
| 3 | `CanDoItAll.Processes.Core` | Contracts, Abstractions | Graph rules, artifact model, state machines, invariants. | Pure unit tests for graph, artifacts, loop fingerprints, transitions. | EF/Razor/domain term dependency appears. |
| 4 | `CanDoItAll.Processes.Templates` | Core, Abstractions | JSON schemas, component refs, overrides, migrations, index model. | Schema, migration chain, merge/conflict, projection hash tests. | Markdown/Mermaid becomes canonical. |
| 5 | `CanDoItAll.Git` | shared primitives only | Typed Git wrapper and path authorization. | Status/diff/commit/merge/conflict/path/log sanitization tests. | Process-specific logic appears. |
| 6 | `CanDoItAll.Processes.Builder` | Core, Templates, Driver abstractions, Git as needed | Compiler pipeline and immutable instance plan persistence contract. | Driver stack, strategy binding, subprocess recursion, plan hash, failure diagnostics. | Runtime performs missing composition. |
| 7 | `CanDoItAll.Processes.Runtime` | Core, Builder contracts, Driver abstractions | Runtime state transitions, scheduler, dispatcher contracts, manager runtime, event emission. | State machine, event, claim lease, cancellation, idempotency, budget tests. | Dispatcher contains domain recovery decisions. |
| 8 | `CanDoItAll.Processes.Persistence` | Core, Runtime abstractions | EF/event store, runtime state tables, artifact ledger, projection stores, indexes. | Repository, concurrency, event/outbox, migration, replay tests. | UI or concrete domain driver reference appears. |
| 9 | `CanDoItAll.Processes.Application` | Builder, Runtime, Persistence, Templates | Use cases and authorization around definitions, runs, templates, projections. | API/use-case, authorization, transaction, error mapping tests. | Application bypasses runtime transitions. |
| 10 | `CanDoItAll.Processes.Drivers.Abstractions` | Core, Abstractions | Driver descriptors, packages, strategy factories, facets, policy fragments. | Compatibility, capability matching contract tests. | Runtime-specific mutation contract appears. |
| 11 | `CanDoItAll.Processes.Drivers.*` | Driver abstractions | Concrete driver packages and strategies. | Driver contract, strategy result, diagnostic redaction, negative fixtures. | Generic runtime code changes required for a driver. |
| 12 | `CanDoItAll.Components.Git` | `CanDoItAll.Git` contracts and UI component base libs | Generic Git status/diff/commit/merge/conflict components. | Component tests and accessibility/authorization state tests. | Process-specific UI assumptions appear. |
| 13 | `CanDoItAll.Modules.Processes` | Application, projection contracts, Git components | Blazor Process UI rebuilt over projections. | Component tests, Playwright live/history/canvas/template flows. | UI reads EF runtime internals or computes runtime truth. |

## Cross-Cutting Gates

- Architecture dependency tests run after every project boundary change.
- Domain vocabulary leak tests run for core/runtime/builder.
- Public DTO schema version tests run for contracts and projections.
- Migration dry-runs run before template or persistence schema changes.
- Every project adds negative tests for the highest-risk failure mode it introduces.

## Required E2E Scenarios Before Final Rewrite Closure

- Generic process with normal, approval, branch, and end steps.
- Subprocess step with artifact import/export and parent/child manager messages.
- Missing artifact recovery and resupply.
- Backward branch route hitting loop budget and escalation.
- Live Processes last-hour view excludes old completed events while keeping active runs visible.
- Template global component update with local override conflict and manual resolution.
- Git manager audit detects unauthorized agent mutation.
- Representative software-delivery flow using layered drivers.

## Stop Rules

- Stop if a future implementation reintroduces the old dispatcher as a wrapped service.
- Stop if runtime selects strategies dynamically instead of using the plan.
- Stop if UI reads runtime EF entities directly.
- Stop if template migrations skip intermediate schema versions.
- Stop if branch routing depends on free-text outcome names.
- Stop if raw diagnostics are displayed directly in normal UI projections.
