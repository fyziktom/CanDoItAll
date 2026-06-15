# Source Prompt Coverage

| Source prompt topic | Preserved where | Notes |
| --- | --- | --- |
| Current processes unreliable but informative | `analysis/01-current-state.md`, `analysis/04-current-code-evidence-map.md` | Current code is evidence, not target architecture. |
| UI/UX direction as anchor | `analysis/05-reuse-decision-log.md`, `analysis/06-current-implementation-user-story-map.md`, `architecture/08-monitoring-events-snapshots-and-ui-projections.md` | UI consumes projections and preserves live/history/canvas direction with explicit US-### coverage. |
| Old drivers not assumed correct | `analysis/05-reuse-decision-log.md`, `architecture/06-driver-strategy-and-manager-model.md` | Existing drivers are adapted into a broader driver package model. |
| Process module as operating system | `architecture/01-target-solution.md`, `architecture/03-core-model-and-invariants.md` | Kernel, scheduler, dispatcher, manager, drivers, artifact file system, and observability are separated. |
| Generic core and layered drivers | `architecture/03-core-model-and-invariants.md`, `architecture/06-driver-strategy-and-manager-model.md` | Core has opaque capability tags; driver hierarchy owns domain terms. |
| Builder and factories | `architecture/04-builder-and-instance-composition.md` | Builder acts as compiler and persists immutable instance plans. |
| Subprocess step composition | `architecture/04-builder-and-instance-composition.md`, `architecture/07-artifact-error-recovery-and-subprocess-model.md` | Child plans are recursively built and linked to parent step refs. |
| Strategy pattern for runtime behavior | `architecture/06-driver-strategy-and-manager-model.md` | Strategy families and binding/execution boundaries are defined. |
| Step can be normal, process, workflow, agent, multi-agent, handoff | `architecture/04-builder-and-instance-composition.md`, `architecture/06-driver-strategy-and-manager-model.md` | All execution kinds require build-time strategy binding. |
| Completed steps cannot be discarded | `architecture/07-artifact-error-recovery-and-subprocess-model.md` | Artifact ledger retains results and references through retention policy. |
| Artifact ownership/sharing/availability/dependency/recovery/resupply | `architecture/07-artifact-error-recovery-and-subprocess-model.md` | Artifact definitions, slots, instances, refs, ledger, access, freshness, validation, and lineage are explicit. |
| Parent/child artifact refs | `architecture/07-artifact-error-recovery-and-subprocess-model.md` | Import/export projection policies guard cross-process artifacts. |
| Error handling beyond happy path | `architecture/07-artifact-error-recovery-and-subprocess-model.md` | Fault layers and manager incidents are defined. |
| Raw errors too detailed for users | `architecture/07-artifact-error-recovery-and-subprocess-model.md`, `architecture/10-security-governance-and-agent-change-auditing.md` | Raw diagnostics become restricted evidence and user-safe incidents. |
| Configured automatic recovery | `architecture/06-driver-strategy-and-manager-model.md`, `architecture/07-artifact-error-recovery-and-subprocess-model.md` | Policy, approval, idempotency, budget, and fingerprint gates are required. |
| Parent/subprocess manager communication | `architecture/07-artifact-error-recovery-and-subprocess-model.md` | Durable generic control messages are defined. |
| Manager responsibilities | `architecture/06-driver-strategy-and-manager-model.md`, `architecture/07-artifact-error-recovery-and-subprocess-model.md` | Inputs, outputs, decisions, recovery, escalation, and loop limits are explicit. |
| Monitoring/live/history/snapshots | `architecture/08-monitoring-events-snapshots-and-ui-projections.md` | Event store, outbox, projectors, offsets, snapshots, dead letters, and time filters are defined. |
| Live mode cache behavior | `architecture/08-monitoring-events-snapshots-and-ui-projections.md` | Live reads snapshot cache; force refresh reads projections, not runtime internals. |
| Template modularization | `architecture/09-template-git-versioning-and-migrations.md` | Components, local overrides, publishing, and conflicts are modeled. |
| Git-like conflict resolution | `architecture/09-template-git-versioning-and-migrations.md` | Three-way merge and conflict records are defined. |
| Template versioning and migrations | `architecture/09-template-git-versioning-and-migrations.md` | Schema/content versions and sequential migration chain are defined. |
| JSON source of truth | `architecture/09-template-git-versioning-and-migrations.md` | Markdown/Mermaid are generated/exported projections only. |
| Git wrapper and Git UI | `architecture/09-template-git-versioning-and-migrations.md`, `architecture/10-security-governance-and-agent-change-auditing.md` | Git wrapper operations, security, process audit, and generic Git components are defined. |
| Switch/branch steps | `architecture/03-core-model-and-invariants.md`, `architecture/06-driver-strategy-and-manager-model.md` | Generic branches, driver families, and user overrides are covered. |
| Backward routes and loop protection | `architecture/05-runtime-dispatcher-and-state-machines.md`, `architecture/07-artifact-error-recovery-and-subprocess-model.md` | Loop budgets, fingerprints, and escalation are required. |
| Future rewrite branch | `plan/02-phase-0-reference-archive-and-removal.md` | Preflight requires a rewrite branch and clean working tree. |
| Archive old implementation before removal | `plan/02-phase-0-reference-archive-and-removal.md` | Reference archive path, manifest, hashes, and commit plan are defined. |
| Remove old projects/tests before rebuild | `plan/02-phase-0-reference-archive-and-removal.md` | Deletion categories and search proof are explicit. |
| Identify reusable current parts | `analysis/05-reuse-decision-log.md` | Major surfaces have archive/adapt/drop/replace decisions. |
| Test strategy for rebuild | `validation/02-architecture-test-plan.md`, `plan/03-project-by-project-rebuild-plan.md` | Tests are project-by-project and gate-by-gate. |
| Bundle versioning | `README.md`, `.gitignore` evidence in `inputs/01-source-artifacts.md` | v2 exists as a versioned bundle and `.gitignore` exceptions remain. |
| v3 request for whole roadmap and subbundles | `plan/04-future-subbundle-roadmap.md`, `subbundles/01-*` through `subbundles/28-*` | v3 prepares detailed future implementation packages but does not execute them. |
| User-story map improvement request | `inputs/03-user-story-map-request.md`, `analysis/06-current-implementation-user-story-map.md`, `architecture/18-user-story-coverage-model.md`, `traceability/04-user-story-coverage-map.md`, `validation/04-user-story-coverage-validation.md` | Current code, tests, templates, and live UI evidence are mapped to US-001 through US-055. |
| Split complex UI rebuild into smaller parts | `plan/01-phase-plan.md`, `plan/04-future-subbundle-roadmap.md`, `subbundles/13-*` through `subbundles/28-*` | Browser-facing subbundles require Playwright MCP proof and screenshots at the owning subbundle gate. |
| v3 project-order correction | `architecture/11-project-boundary-and-dependency-map.md`, `plan/03-project-by-project-rebuild-plan.md` | Driver abstractions precede Builder; projections precede UI. |
| v3 persistence/event-store decision | `architecture/12-runtime-persistence-event-store-and-outbox.md` | Runtime uses ports; EF/PostgreSQL lives in Persistence. |
| v3 typed branch contract | `architecture/13-branch-switch-and-loop-contract.md` | Free-text token routing is rejected. |
| v3 manager loop | `architecture/14-manager-runtime-and-control-loop.md` | Manager queue, decisions, policy order, recovery, subprocess messages, and anti-dispatcher rules are explicit. |
| v3 adapter boundaries | `architecture/16-execution-adapters-and-integration-boundaries.md` | Workflow/agent/handoff/scheduler/project/plugin integrations are strategies/adapters. |
| v3 runtime history compatibility | `architecture/17-runtime-history-migration-and-readonly-compatibility.md` | Historical runs get inventory and migration/archive/read-only options. |
