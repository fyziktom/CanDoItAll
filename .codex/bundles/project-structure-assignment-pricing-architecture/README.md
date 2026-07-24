# Project Structure Assignment and Pricing Architecture

This bundle is a coordination and execution package for `project-structure-assignment-pricing-architecture`.

## Profile

- `initiative`

## Mission

- Make Project Structure tasks editable when they legitimately have both person and AI-agent assignments, make unstarted-task cost authoritative to CRM or the selected automated-resource estimator, and replace duplicated/switch-heavy Project Structure logic with independently testable assignment and pricing collaborators.

## Outcome Contract

- Requested outcome: mixed-assignee tasks open and save safely; task prices are refreshed from the correct resource source while the task is unstarted; cost estimation is extended through strategies; the Project Structure partial cluster owns less behavior.
- Hard constraints: preserve every unrelated Project Structure capability; no silent pricing fallback; no new partial class as an architecture boundary; no stringly typed resource routing; no project-reference cycle.
- Evidence required before closure: isolated strategy/resolver tests, realistic positive and negative lifecycle tests, mixed-assignment preservation proof, affected component tests, Workbench/app builds, a large-screen Gantt dialog browser smoke when the local host can run, and a passing C# architecture gate.
- Known blockers or explicit scope exceptions: CodeAnalytics MCP is unavailable in this session, so symbol, dependency, and partial-class evidence is gathered from `rg`, exact source reads, project files, compiler output, and tests. No responsive redesign is in scope.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries when architecture decisions are material
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts when repeated handoff needs them
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-task-assignment-and-cost-strategy-foundation`
2. `subbundles/02-authoritative-task-pricing-and-gantt-behavior`
3. `subbundles/03-project-structure-regression-and-architecture-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## UI Target Policy

- CanDoItAll applications target large-screen desktop use; do not add small/medium/mobile tuning unless explicitly requested.
- Reusable basic `CanDoItAll.Components.BaseLib` components remain responsible for small, medium, and large viewport behavior.

## Validation Summary

- Bundle preparation status: `Prepared — canonical validator passed 2026-07-23`
- Execution status: `Completed`
- Subbundle gate review: `Passed — SB01/SB02 Behavioral proof, SB03 regression/browser evidence, and the independent C# architecture review agree`
- Final closure gate: `Passed with non-blocking follow-up — the narrow bridge shape and future bulk delete/move assertion gap are recorded in the architecture gate`
- Browser validation analytics: `Passed — PostgreSQL-backed mixed Person + Agent Gantt dialog inspected at 1600x1000; normal and open-overlay screenshots reviewed`

See `reviews/01-execution-report.md` for the exact proof matrix, shallow-pass traps, and completed closure record. The initial combined page-preservation run is not claimed as passing; its deterministic test-harness hang was repaired in tests only, and all 35 cases passed across four explicit split invocations.
