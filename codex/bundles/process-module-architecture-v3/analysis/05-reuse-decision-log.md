# Reuse Decision Log

## Decision Rules

- `archive`: copy as reference and do not port behavior directly.
- `adapt`: reuse the concept after redesigning target contracts.
- `drop`: remove after archive because it encodes the wrong architecture.
- `replace`: build a new target mechanism that covers the requirement.

| Current surface | Decision | Reason | Target destination | Risk |
| --- | --- | --- | --- | --- |
| `ProcessRunAutomationDispatchService` | archive | Mixes EF, claim lifecycle, agent execution, workflow execution, artifacts, validation, provider fallback, browser proof, prompt construction, and finalization. | `Processes.Runtime` dispatcher plus strategies, manager, and drivers built from clean contracts. | High risk of accidental rewrap because it contains many working edge cases. |
| `ProcessesService.StartRunAsync` path | adapt | Useful transactional start, assignments, outbox, journals, parent/root/depth, and subprocess validation. Composition is not first-class. | `Processes.Builder` creates immutable plan; `Processes.Runtime` creates initial runtime state transactionally. | Hidden dependencies may surface when removing service-side start behavior. |
| `ProcessDefinitionEntities` | adapt | Contains roles, steps, dependencies, branch outcomes, artifact expectations, and subprocess refs. Persistence shape is not target source model. | Template source, definition snapshot, and migration adapters. | Existing DB data may require compatibility import tooling. |
| `ProcessRuntimeModels` | adapt | Contains runs, step runs, assignments, decisions, artifacts, journals, launch plans, workflow links, claim tokens, leases, and attempts. | Runtime state tables, event store, artifact ledger, launch-plan import. | Old entity assumptions may leak into new contracts if migration is rushed. |
| `ProcessCanvasBranching` | adapt | Useful canvas and branch-router UI concepts. Runtime semantics must become typed. | UI canvas projections and branch editor models. | UI regression if target projections do not support existing canvas affordances. |
| `ProcessBranchOutcomeRouting` | replace | Text-token routing is fragile and not authorable at scale. | Typed branch definitions, branch families, manager decision records, and loop fingerprints. | Existing templates relying on text conventions need migration diagnostics. |
| `ProcessRecoveryRouter` | adapt | Useful no-progress fingerprinting and recovery decision shape. Too coupled to agent-centric recovery options. | Generic manager/recovery strategy model and loop budget ledger. | Over-generalizing recovery could hide actionable domain signals. |
| `AgentRecoveryModels` | adapt | Useful rework packet, proof requirement, recovery ledger, and loop decision concepts. | Manager incidents, recovery requests, and strategy envelopes. | Must avoid importing agent-only terminology into core. |
| `ProcessObservationCache` | adapt | Useful cache keys, invalidation, and freshness concept. Not durable truth. | Projection cache and live snapshot cache. | Cache invalidation bugs can reappear if projection offsets are underspecified. |
| `ProcessObservationService` | archive | Good UX projection reference, but query-built and too close to current runtime internals. | Projection workers and UI read models. | Time-window bugs can persist if filters are not tested at projection/query boundary. |
| `LiveProcessesDashboard.razor` | adapt | Useful UI/UX direction for live process view, filters, summary, activity, agents, metrics, and tool analytics. | `Modules.Processes` projection-only UI. | Backend rewrite may remove data the UI expects unless projection contracts are explicit. |
| `ProcessTemplatePackLoader` | adapt | Correct file-first direction and shared/local resource loading. Missing version/override/migration model. | `Processes.Templates` canonical loader and migration chain. | Sidecars may be mistaken as canonical during migration. |
| `ProcessTemplateProjectionService` | replace | Current projection shape supports import envelopes but not target source-of-truth rules. | Generated projection/export services outside canonical template source. | Stored projections can drift unless hashes and generation metadata are enforced. |
| `ProcessCoreArtifactModels` | adapt | Useful trust and sensitivity vocabulary. Too small for target artifact OS. | `Processes.Core` artifact definitions, slots, instances, references, access policy, and ledger concepts. | Premature enum reuse can lock the target model into old assumptions. |
| `ProcessArtifactExpectationMatcher` | adapt | Pure, testable, generic rule with diagnostics. | Core artifact resolver/matcher tests after artifact model redesign. | Needs stronger matching around slots, lineage, scope, and freshness. |
| Existing driver abstraction package | adapt | Contains useful permissions, evidence, audit, redaction, and verification contracts. | `Processes.Drivers.Abstractions` driver package model. | Current capability enums/values must not become core concepts. |
| Existing verification drivers | adapt | Useful domain diagnostics and fake-proof resistance work. Too narrow as complete driver architecture. | Driver-provided validation strategies and domain facets. | Driver layer may stay verification-only unless strategy provisioning is designed first. |
| Existing Process tests | adapt | Good regression evidence across drivers, canvas, runtime, observation, templates, subprocesses, Playwright smoke. | Quarantine/reference, then rebuild as contract, integration, component, and E2E tests per project. | Tests compiling against old entities can block removal if not quarantined cleanly. |
| `Templates/Processes` | adapt | Large existing JSON process library with shared and local resources. | Migration input for Git-backed canonical template files. | Must not be deleted during Phase 0. |
| Markdown/Mermaid template sidecars | drop | Useful as generated documentation only; not reliable canonical behavior. | Generated/exported projections with source hashes. | Users may have manually edited sidecars; migration must report drift. |
| Current module projection sidecars | replace | Useful migration evidence but not target architecture. | Template projection metadata and compatibility reports generated from canonical JSON. | Projection drift can corrupt imports if treated as source. |
