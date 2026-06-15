# Current Code Evidence Map

## Purpose

This map records source evidence used by the v2 architecture. The conclusion is direct: the old implementation contains valuable domain knowledge, but the runtime/dispatcher shape is not a safe foundation for the rewrite.

## Solution And Project Surface

| Evidence | Observation | Architectural use |
| --- | --- | --- |
| `repo://CanDoItAll.slnx` | Process projects exist for module UI/runtime, contracts, core, driver abstractions, and several driver implementations. | Confirms there is an existing split, but v2 must redefine project boundaries instead of inheriting them blindly. |
| `repo://src/CanDoItAll.Modules.Processes` | Contains Razor components, services, persistence entities, runtime, dispatch, recovery, templates, canvas, observation, and agent tools. | Archive as reference; future UI module must become a projection consumer. |
| `repo://src/CanDoItAll.Processes.Core` | Contains pure-ish artifact, routing, finalization, diagnostics, and subprocess rules. | Keep as conceptual direction; review each type before porting into the target core. |
| `repo://src/CanDoItAll.Processes.Drivers.*` | Current drivers are evidence/verification oriented. | Adapt into a broader driver package model with capabilities, strategies, policies, branch definitions, recovery handlers, and facets. |
| `repo://Templates/Processes` | Contains JSON definitions and many Markdown/Mermaid/projection sidecars. | Treat JSON content as migration input; generated projections are not source of truth in v2. |
| `repo://.gitignore` | Allows `codex/bundles/process-module-architecture*/**` while keeping unrelated bundles ignored. | Satisfies bundle versioning requirement. |

## Dispatcher Coupling

`repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs` depends on EF, service scopes, technical agent bridge, process execution client, storage placement/catalog/driver services, workspace path resolver, database profile accessor, workflow coordinator, options, clock, and logger.

It also defines static tool/proof constants and domain-ish behavior for workspace tools, browser evidence, implementation proof, product mutation, provider fallback, mock role keys, and recovery timing.

Decision: archive only. The target dispatcher may reuse lessons about leases, duplicate suppression, and result normalization, but must not wrap this service.

## Run Start And Subprocess Handling

`repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` creates `ProcessRun`, assignments, step runs, work briefs, journal entries, project-structure sync, outbox records, and observation invalidation inside one service path.

It validates subprocess parent run and parent step identifiers, parent run terminal states, hierarchy depth, parent step kind, target definition compatibility, cycle prevention, and existing subprocess run reuse.

Decision: adapt concepts into the builder and runtime bootstrap. The target builder must recursively create child instance plans before runtime starts. Runtime may reuse parent/root/depth invariants but should not perform first-time composition inside a start service.

## Definition And Runtime Entities

`repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessDefinitionEntities.cs` contains useful concepts: definitions, versions, roles, role skills, messaging policy, steps, dependencies, branch outcomes, role assignment requirements, artifact expectations, and step artifact inputs.

`repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs` contains useful concepts: process runs, step runs, assignments, work briefs, decisions, artifact records, journal entries, conformance observations, improvement candidates, launch plans, workflow links, parent/root/depth fields, claim token fields, lease expiry, and attempt counts.

Decision: migration input only. The target separates template source, definition snapshot, immutable instance plan, mutable runtime state, append-only events, artifact ledger, and UI projections.

## Branching

`repo://src/CanDoItAll.Modules.Processes/Canvas/ProcessCanvasBranching.cs` contains useful canvas ideas for default/error outcomes, branch router rendering, node/port mapping, and normalization.

`repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessBranchOutcomeRouting.cs` detects exception routing through `ProcessCanvasBranching.IsErrorOutcome` and normalized text tokens such as repair, remediation, rework, defect, failed validation, escalation, no-go, and blocked.

Decision: preserve UI/canvas concepts, replace runtime semantics. Target branches use typed definitions, branch families, route targets, manager decisions, and loop budgets.

## Recovery

`repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRecoveryRouter.cs` has useful recovery request/decision records, evidence fingerprints, no-progress guard behavior, and preferred recovery options for missing artifacts, validation failures, no progress, missing credentials, tool unavailability, policy denial, manual rerun, and agent execution failure.

Decision: adapt no-progress fingerprinting and budget ideas into generic manager/recovery strategies. Do not port agent-centric recovery options into the core.

## Observation And Monitoring

`repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationCache.cs` uses a memory cache keyed by observation kind, project, definition, run, step, definition set, and query fingerprint. It supports cache invalidation by project, definition, and run.

`repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` builds dashboard, live, run, stage, timeline, and dialog snapshots. It includes live history windows, active run inclusion, escalation cards, agent cards, metric buckets, and tool usage summaries.

Decision: adapt UX/query shapes and cache behavior. Replace query-built runtime truth with durable event store, projector offsets, current snapshots, historical projections, and strict time-range query semantics.

## Templates

`repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs` loads `manifest.json`, framework sources, toolbox role/step templates, chrome actions, baseline scenarios, live run profiles, shared roles/artifacts/checklists/validations/prompts, process-local resources, `definition.json`, `definition.md`, current-module projection files, and Mermaid flow/sequence files.

Decision: adapt JSON pack loading direction. Replace sidecar-as-state behavior with JSON canonical files, generated/exported Markdown/Mermaid projections, component version metadata, local override patches, conflict records, deterministic migrations, and database indexing.

## Pure Core Examples

`repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessCoreArtifactModels.cs` contains artifact kind, trust requirement/status, sensitivity, expectation snapshots, and record snapshots.

`repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactExpectationMatcher.cs` is small, pure, generic, and returns diagnostics for strong matches, ambiguous matches, and kind disambiguation.

Decision: keep/adapt after model redesign. The matcher style is the target shape for core rules, but the artifact model needs slots, references, ledger, lineage, availability, freshness, validation, access policy, and recovery/resupply state.

## Tests

`repo://tests` contains unit, component, integration, and Playwright tests for process drivers, contracts, canvas, runtime, observation, dispatch, templates, subprocesses, and launch flows.

Decision: use as regression reference and source evidence. Tests that compile against old runtime models should be quarantined or rewritten during Phase 0 and project rebuild.
