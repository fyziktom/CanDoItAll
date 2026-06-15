# Requirement Traceability

## Requirement Matrix

| Requirement ID | Requirement summary | Source prompt clause | Architecture section | Plan/gate | Validation / acceptance |
| --- | --- | --- | --- | --- | --- |
| REQ-001 | Generic Process Core contains only domain-neutral concepts. | Core remains generic; domain terms isolated behind drivers. | `architecture/03-core-model-and-invariants.md` | `plan/03-project-by-project-rebuild-plan.md` order 3 | AC-001; architecture dependency and vocabulary leak tests. |
| REQ-002 | Runtime Engine owns state transitions, scheduling, cancellation, persistence coordination, and runtime events. | Separate runtime execution from dispatcher/UI. | `architecture/05-runtime-dispatcher-and-state-machines.md` | G06 | AC-006; runtime transition/event tests. |
| REQ-003 | Dispatcher claims executable work, invokes assigned strategy, returns result to runtime. | Dispatcher responsibilities separated from runtime and domain recovery. | `architecture/05-runtime-dispatcher-and-state-machines.md` | G06 | AC-007; lease/idempotency tests. |
| REQ-004 | Process instance composition is explicit build product. | Process instance assembled for a specific run. | `architecture/04-builder-and-instance-composition.md` | G05 | AC-004; builder persisted-plan tests. |
| REQ-005 | Definition, template, instance, runtime, event, snapshot, and UI models are separate. | Architecture must clearly separate all planes. | `architecture/03-core-model-and-invariants.md` | G03, G09 | AC-003; architecture and projection access tests. |
| REQ-006 | Support layered domain drivers without domain vocabulary in core. | Driver hierarchy example with broad and narrow drivers. | `architecture/06-driver-strategy-and-manager-model.md` | G07 | AC-010; driver stack and domain leakage tests. |
| REQ-007 | Drivers provide capabilities, strategy factories, branch definitions, recovery policies, manager policies, and template fragments. | Core generic but able to use specific drivers. | `architecture/06-driver-strategy-and-manager-model.md` | G07 | AC-010; driver package contract tests. |
| REQ-008 | Assign step execution strategies at build time for all execution kinds. | Builder assigns correct execution strategy to each step. | `architecture/04-builder-and-instance-composition.md`, `architecture/06-driver-strategy-and-manager-model.md` | G05 | AC-004, AC-011; strategy binding snapshot tests. |
| REQ-009 | Use strategy interfaces for execution, manager, recovery, resupply, error preprocessing, branch decisions, subprocess communication, and loop escalation. | Strategy pattern required for runtime and manager behavior. | `architecture/06-driver-strategy-and-manager-model.md` | G07, G08 | AC-011; strategy family contract tests. |
| REQ-010 | Build process instances from definitions/templates and run context. | Instance assembled based on what is needed for a specific run. | `architecture/04-builder-and-instance-composition.md` | G05 | AC-004; builder pipeline tests. |
| REQ-011 | Compose roles, artifacts, steps, subprocesses, drivers, strategies, recovery, branch, manager, and monitoring config. | Builder composition list from source prompt. | `architecture/04-builder-and-instance-composition.md` | G05 | AC-004; golden plan includes every section. |
| REQ-012 | Recursively build subprocess instances. | Step can be another process; subprocess builder executed. | `architecture/04-builder-and-instance-composition.md`, `architecture/07-artifact-error-recovery-and-subprocess-model.md` | G05, G08 | AC-005; subprocess recursion tests. |
| REQ-013 | Enforce subprocess depth, cycle, and compatibility checks during composition. | Subprocesses may nest but need safeguards. | `architecture/04-builder-and-instance-composition.md` | G05 | AC-005; depth/cycle negative tests. |
| REQ-014 | Persist composed instance plan before execution. | Step strategy part of composition, not runtime afterthought. | `architecture/04-builder-and-instance-composition.md` | G05 | AC-004; runtime refuses missing plan. |
| REQ-015 | Model artifact ownership, sharing, availability, dependency, recovery, resupply, and cross-process references. | Artifact lifecycle and sharing required. | `architecture/07-artifact-error-recovery-and-subprocess-model.md` | G08 | AC-014; artifact scope/reference tests. |
| REQ-016 | Retain completed step results and artifact ledgers for later consumers. | Completed steps cannot be discarded. | `architecture/07-artifact-error-recovery-and-subprocess-model.md` | G08 | AC-014; ledger retention tests. |
| REQ-017 | Later steps, branches, managers, and subprocesses can reference earlier artifacts when permitted. | Final step may need artifacts from first and other steps. | `architecture/07-artifact-error-recovery-and-subprocess-model.md` | G08 | AC-014; cross-step consumer tests. |
| REQ-018 | Missing artifact handling is manager/driver/strategy-driven. | Manager may ask agent or use driver. | `architecture/07-artifact-error-recovery-and-subprocess-model.md` | G08 | AC-015; missing artifact incident tests. |
| REQ-019 | Track provenance, trust, sensitivity, retention, freshness, and validation. | Artifact model must include these lifecycle properties. | `architecture/07-artifact-error-recovery-and-subprocess-model.md` | G08 | AC-014; artifact metadata tests. |
| REQ-020 | Model runtime errors and domain diagnostics separately. | Must not focus only on happy path. | `architecture/07-artifact-error-recovery-and-subprocess-model.md` | G08 | AC-016; fault classification tests. |
| REQ-021 | Preprocess detailed errors into user-actionable manager incidents. | Raw agent/subprocess errors may be too much for UI. | `architecture/07-artifact-error-recovery-and-subprocess-model.md`, `architecture/10-security-governance-and-agent-change-auditing.md` | G08 | AC-016; incident preprocessing/redaction tests. |
| REQ-022 | Support configured automatic recovery for selected errors through strategies. | User can configure manager automatic resolution. | `architecture/06-driver-strategy-and-manager-model.md`, `architecture/07-artifact-error-recovery-and-subprocess-model.md` | G08 | AC-017; recovery eligibility tests. |
| REQ-023 | Prevent uncontrolled recovery loops with budgets and escalation. | Manager prevents loops and enforces limits. | `architecture/05-runtime-dispatcher-and-state-machines.md`, `architecture/07-artifact-error-recovery-and-subprocess-model.md` | G08 | AC-017; loop fingerprint/budget tests. |
| REQ-024 | Generic process manager uses domain behavior through strategies/drivers. | Manager central but not domain-hardcoded. | `architecture/06-driver-strategy-and-manager-model.md` | G08 | AC-012; manager strategy tests. |
| REQ-025 | Parent and subprocess managers communicate. | Parent manager commonly communicates with subprocess manager. | `architecture/07-artifact-error-recovery-and-subprocess-model.md` | G08 | AC-013; durable control message tests. |
| REQ-026 | Emit typed runtime events for state changes and decisions. | Runtime event emission required. | `architecture/05-runtime-dispatcher-and-state-machines.md`, `architecture/08-monitoring-events-snapshots-and-ui-projections.md` | G06, G09 | AC-008; event envelope tests. |
| REQ-027 | Observers/subscribers do not block runtime execution. | Monitoring layer must not slow actual processes. | `architecture/08-monitoring-events-snapshots-and-ui-projections.md` | G09 | AC-018; projector isolation tests. |
| REQ-028 | Maintain current/live snapshot cache and historical projections. | LiveProcesses should use latest snapshots/cache. | `architecture/08-monitoring-events-snapshots-and-ui-projections.md` | G09 | AC-019; snapshot/history replay tests. |
| REQ-029 | Apply time-range filters correctly. | Last hour must not show stale older events unless requested. | `architecture/08-monitoring-events-snapshots-and-ui-projections.md` | G09 | AC-020; live/history time-window tests. |
| REQ-030 | Provide UI-friendly live/history read models. | UI-facing projections and live/history views. | `architecture/08-monitoring-events-snapshots-and-ui-projections.md` | G09 | AC-021; UI projection contract tests. |
| REQ-031 | JSON is source of truth for templates/config. | Source of truth should definitely be JSON. | `architecture/09-template-git-versioning-and-migrations.md` | G04 | AC-022; schema tests. |
| REQ-032 | Markdown/Mermaid are generated/exported projections. | Architecture must decide and justify projections. | `architecture/09-template-git-versioning-and-migrations.md` | G04 | AC-023; projection hash drift tests. |
| REQ-033 | Support global components and local overrides. | Reuse role/artifact/step definitions across processes. | `architecture/09-template-git-versioning-and-migrations.md` | G04 | AC-024; override patch tests. |
| REQ-034 | Publish global updates, detect conflicts, resolve manually. | Git-like update/conflict workflow required. | `architecture/09-template-git-versioning-and-migrations.md` | G04 | AC-024; three-way merge/conflict UI tests. |
| REQ-035 | Version template schemas and content. | Templates need version markings. | `architecture/09-template-git-versioning-and-migrations.md` | G04 | AC-025; version compatibility tests. |
| REQ-036 | Deterministic migrations handle skipped intermediate versions. | Skipped migration wave may fail otherwise. | `architecture/09-template-git-versioning-and-migrations.md` | G04 | AC-025; migration chain tests. |
| REQ-037 | Store template files in Git with database indexing. | Files allow efficient versioning; DB indexing still needed. | `architecture/09-template-git-versioning-and-migrations.md` | G04 | AC-026; index rebuild tests. |
| REQ-038 | Create typed Git wrapper project. | Do not implement own Git. | `architecture/09-template-git-versioning-and-migrations.md` | Rebuild order 5 | AC-026; Git wrapper tests. |
| REQ-039 | Use Git wrapper for templates, instructions, skills, workflows, processes, run change tracking. | Git wrapper unavoidable for configuration texts. | `architecture/09-template-git-versioning-and-migrations.md`, `architecture/10-security-governance-and-agent-change-auditing.md` | Rebuild order 5, 9 | AC-026; Git integration tests. |
| REQ-040 | Manager verifies unauthorized agent modifications. | Manager checks whether agent modified something it should not. | `architecture/10-security-governance-and-agent-change-auditing.md` | Rebuild order 9 | AC-026; agent change audit tests. |
| REQ-041 | Provide reusable Git UI components. | Generic Git UI components required. | `architecture/09-template-git-versioning-and-migrations.md` | Rebuild order 12 | AC-026; component tests. |
| REQ-042 | Support generic branch/switch steps. | Fully generic branch/switch mechanism. | `architecture/03-core-model-and-invariants.md`, `architecture/06-driver-strategy-and-manager-model.md` | G05, G08 | AC-011; generic branch tests. |
| REQ-043 | Support domain branch definitions and user overrides. | Domain-specific branch definition and hybrid override behavior. | `architecture/06-driver-strategy-and-manager-model.md`, `architecture/09-template-git-versioning-and-migrations.md` | G07, G04 | AC-010, AC-024; branch family/override tests. |
| REQ-044 | Allow branch routes to previous steps. | Branches are not only forward. | `architecture/03-core-model-and-invariants.md`, `architecture/05-runtime-dispatcher-and-state-machines.md` | G08 | AC-006; backward route transition tests. |
| REQ-045 | Protect backward routes with loop budgets, fingerprints, escalation. | Repeated failed fixes must escalate. | `architecture/05-runtime-dispatcher-and-state-machines.md`, `architecture/07-artifact-error-recovery-and-subprocess-model.md` | G08 | AC-017; loop escalation tests. |
| REQ-046 | Version architecture bundles by `.gitignore`. | Bundles are in `.gitignore`; update so versionable. | `README.md`, `inputs/01-source-artifacts.md` | This architecture pass | AC-030; `.gitignore` review. |
| REQ-047 | Future implementation starts on new branch. | Later implementation should create a new branch. | `plan/02-phase-0-reference-archive-and-removal.md` | P0 preflight | AC-027; branch preflight proof. |
| REQ-048 | Copy old Process implementation to reference before deletion. | Old implementation first copied into bundle reference material. | `plan/02-phase-0-reference-archive-and-removal.md` | P0-G01 | AC-027; manifest/hash proof. |
| REQ-049 | Remove old Process projects/tests before rebuilding. | Remove original Process implementation including projects/tests. | `plan/02-phase-0-reference-archive-and-removal.md` | P0-G02, P0-G03 | AC-027; search proof. |
| REQ-050 | Rebuild with tests project by project and phase by phase. | Add projects from ground up with tests for each. | `plan/03-project-by-project-rebuild-plan.md` | All project gates | AC-029; per-project test proof. |

## Coverage Notes

- Every normalized requirement maps to at least one architecture section, plan/gate, and validation criterion.
- v2 replaces v1's grouped traceability with requirement-level rows.
- v3 keeps the requirement-level mapping and adds subbundle traceability in `traceability/03-subbundle-traceability.md`.
- Future implementation subbundles are prepared in v3 but not executed.

## v3 Gap Coverage

| v3 gap | Covered by | Acceptance |
| --- | --- | --- |
| Real future subbundles required | `subbundles/01-*` through `subbundles/14-*`, `plan/04-future-subbundle-roadmap.md` | AC-038 |
| Project dependency/order ambiguity | `architecture/11-project-boundary-and-dependency-map.md`, `plan/03-project-by-project-rebuild-plan.md` | AC-031 |
| Runtime persistence/event/outbox detail | `architecture/12-runtime-persistence-event-store-and-outbox.md` | AC-032 |
| Branch/switch contract detail | `architecture/13-branch-switch-and-loop-contract.md` | AC-033 |
| Manager runtime control loop | `architecture/14-manager-runtime-and-control-loop.md` | AC-034 |
| UI/UX projection preservation plan | `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md` | AC-035 |
| Execution adapter boundaries | `architecture/16-execution-adapters-and-integration-boundaries.md` | AC-036 |
| Runtime history compatibility | `architecture/17-runtime-history-migration-and-readonly-compatibility.md` | AC-037 |
| Review checkpoints embedded | `plan/05-review-checkpoints-and-hardening-gates.md`, every subbundle README | AC-038 |
