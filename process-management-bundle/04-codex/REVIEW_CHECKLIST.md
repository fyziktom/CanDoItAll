# Codex review checklist

Use this checklist before considering a process-management feature ready.

## Canonical ownership

- [ ] `CanDoItAll.Modules.Processes` remains the canonical owner of process definitions, runtime state, work briefs, routing decisions, and journals.
- [ ] CRM-HR remains the canonical owner of durable human/AI identities and reusable business role/agent templates.
- [ ] Workspace remains the canonical owner of shared provider profiles.
- [ ] Workbench and live canvas overlays are projection-only surfaces.

## Process-native orchestration

- [ ] Human or agent collaboration is expressed through the modeled process rather than hidden runtime topology.
- [ ] Baton/work-brief semantics are durable where the feature requires them.
- [ ] Triage or routing decisions remain explainable and governed.
- [ ] Break-glass or bypass behavior is explicitly journaled and reviewable.

## Cross-repo convergence

- [ ] No new permanent template/provider/capability registry was introduced in the process bridge layer.
- [ ] No direct compile-time dependency on the external AgentFramework repo was added unless explicitly planned in a later adapter project.
- [ ] Future external executor context is correlated back to process run/step/assignment where relevant.
- [ ] Project context and process orchestration remain separate but linkable.

## Persistence and runtime safety

- [ ] SQLite and PostgreSQL both remain supported where persistence changed.
- [ ] Published versions remain immutable.
- [ ] Runtime journals stay append-oriented.
- [ ] Projection-only overlay data cannot mutate canonical process state directly.

## UX and tests

- [ ] Component and integration tests cover the new behavior.
- [ ] Playwright coverage exists for critical process-authoring or runtime flows when the feature changes full workflows.
- [ ] The UI makes governance warnings, ownership gaps, and state/projection boundaries understandable.
