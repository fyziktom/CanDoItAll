# Process Observation Projection Architecture

This bundle is a planning and execution package for preparing the Processes module to support a flexible, live, read-only observation UI without overloading process runtime services.

## Profile

- `initiative`

## Mission

- Move process observation from page-local polling and ad hoc detail loading toward a typed, cache-aware observation projection boundary. Process core, persisted runtime state, outbox, and AgentFramework execution history remain the source of truth; UI, dialogs, and future AI-driven dashboards consume read-only snapshots and lazy detail payloads.

## Outcome Contract

- Requested outcome: an implementation-ready bundle with subbundles only for refactoring process core/UI communication around observation services, cache policy, Blazor performance, and future AI-assisted dashboard control.
- Hard constraints: no production implementation during bundle preparation; preserve all current Processes page behavior; keep process runtime logic generic; do not make cache a source of truth; use existing BaseLib/CanvasLib component patterns; no Radzen requirement was found in the Processes project.
- Evidence required before closure: current-state map, official Microsoft Learn guidance references, .NET performance scan checklist, subbundle dependency gates, validation plan covering mock-agent process runs and independent simple .NET app builds.
- Known blockers or explicit scope exceptions: this bundle does not build the new flexible dashboard UI or conversational AI UI. It prepares the architecture and phased implementation plan.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-01-current-state-observation-map`
2. `subbundles/02-02-observation-contracts-and-boundary`
3. `subbundles/03-03-projection-cache-and-invalidation`
4. `subbundles/04-04-ui-observation-shell-and-dialogs`
5. `subbundles/05-05-ai-driven-dashboard-intent-bridge`
6. `subbundles/06-06-validation-performance-and-rollout`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Not started`
- Subbundle gate review: `Prepared for later implementation`
- Final closure gate: `Not started`
- Browser validation analytics: `Required only during implementation subbundles 04 and 06`
