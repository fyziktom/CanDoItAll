# Red-Team Gap Review

## Finding 1: Architecture Complexity Can Become Its Own Failure Mode

The design is intentionally comprehensive. That is appropriate for a rewrite of this size, but it creates a risk that future implementation adds too many empty abstractions before behavior is proven.

Mitigation: every new project in `plan/03-project-by-project-rebuild-plan.md` has required tests and stop conditions. Future bundles must implement narrow vertical proof slices after the skeleton and architecture tests exist.

## Finding 2: Hidden Old Dependencies May Be Larger Than The Evidence Map Shows

The current module touches Web, composition, EF, scheduler, workflow, project structure, agents, storage, and tests. Phase 0 may reveal references not listed in v2.

Mitigation: Phase 0 requires preflight inventory and search proof. If hidden dependencies are broad, future Codex must record a blocker instead of reintroducing the old dispatcher.

## Finding 3: Template Migration Risk Is High

`Templates/Processes` contains many JSON files plus Markdown, Mermaid, and projection sidecars. Users may have manually edited generated-looking files. Treating projections as non-canonical can still lose user intent if drift is not detected.

Mitigation: migration tooling must compute source/projection hashes, report drift, and require manual review for sidecars whose content does not match generated output.

## Finding 4: Event Projection Operations Are Non-Trivial

Event-first monitoring solves runtime/UI coupling, but it introduces projector offsets, dead letters, replay semantics, lag visibility, and schema version handling.

Mitigation: projection workers and stores must be implemented before UI rebuild. Live/history tests must prove both active-run inclusion and strict historical time filtering.

## Finding 5: Agent-Backed Manager Reliability Is Unproven

The manager model supports deterministic, agent-backed, and hybrid managers. Agent-backed decisions can be inconsistent or unsafe.

Mitigation: deterministic policy owns permissions, budgets, approvals, and escalation. Agent output must be preprocessed into manager incidents or decisions and policy-checked before runtime transitions.

## Finding 6: Driver Generalization Could Be Overbuilt

Drivers are expanded from verification to capabilities, strategies, policies, branch definitions, recovery handlers, template fragments, and facets. That is powerful, but a broad driver API can become difficult to implement correctly.

Mitigation: implement the first driver stack with a representative narrow path, then add capabilities only when tests require them. Core/runtime must not change for a new concrete driver.

## Finding 7: UI Regression Risk Is Real

The current UI/UX is useful. Rebuilding UI over projections can lose details currently produced by query services.

Mitigation: UI projection contracts must be written before component rewrite. Current `LiveProcessesDashboard`, canvas, run details, template library, and observation models should be used as UX evidence.

## Finding 8: Codex May Reintroduce Old Dispatcher Coupling

The old dispatcher contains many edge cases and will be tempting to wrap.

Mitigation: Phase 0 removes active old projects before behavior rebuild. Architecture tests and search proof must fail if old dispatcher symbols appear outside reference material.

## Finding 9: Runtime Data Migration Scope Is Unknown

v3 adds `architecture/17-runtime-history-migration-and-readonly-compatibility.md` and SB12 to handle runtime history inventory, migration/archive/read-only options, compatibility reports, and final closure gates.

Residual risk: actual persisted production data may still reveal fields or retention requirements not visible from source. SB12 must stop and report if compatibility cannot be proven without old runtime code.

## Finding 10: Subbundle Execution Could Drift If Context Is Not Reset

SB01-SB28 are detailed, but future Codex runs can still drift if they skip context reset files, previous execution reports, or the user-story coverage map.

Mitigation: every subbundle README includes context reset files, prerequisites, proof requirements, stop conditions, and handoff notes. Future implementation must execute one approved subbundle at a time unless the user explicitly authorizes combining work. Each subbundle report must record owned US-### story coverage.

## Finding 11: Story Coverage Can Become A Checkbox Exercise

The user-story map can be misused as a superficial checklist if future agents mark stories covered without source, test, and browser proof.

Mitigation: `validation/04-user-story-coverage-validation.md` requires source proof, test proof, browser proof for browser-facing stories, explicit delta decisions, and final US-001 through US-055 closure in SB28.

## Finding 12: Clean Architecture Can Still Be Slow

The rewrite can satisfy dependency boundaries while still recreating slow behavior through allocation-heavy projectors, unbounded event queues, uncached serializers, load-all UI queries, or sync-over-async in worker code.

Mitigation: `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` define hot paths, forbidden antipatterns, exact scan counts, and stop conditions. Gate J in `plan/05-review-checkpoints-and-hardening-gates.md` makes this a closure requirement.
