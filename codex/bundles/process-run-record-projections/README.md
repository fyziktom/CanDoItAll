# Process Run Record Projections

This bundle is a coordination and execution package for `process-run-record-projections`.

## Profile

- `initiative`

## Mission

- Introduce a durable, compact, indexed process-run record that is assembled once from canonical runtime and Agent Framework evidence, enriched asynchronously with a structured manager summary, and becomes the default source for historical lists, details, graphs, analytics, API consumers, and terminal project-structure nodes.

## Outcome Contract

- Requested outcome: historical process information loads from compact records instead of repeatedly hydrating full runtime, assignment, event, and Agent Framework detail stores.
- Hard constraints: strong C# contracts; ID references instead of ORM relationships; explicit completeness and summary-generation states; no LLM call on runtime completion or a read path; runtime remains independent of projections; APIs and the authoritative SharedInfo API skill remain aligned.
- Evidence required before closure: prepared-bundle validation, focused unit/integration tests, migration/model validation, API contract tests, architecture review gate, two-pass performance review, solution build, and final bundle validation.
- Known blockers or explicit scope exceptions: historic records created before this feature can only be backfilled to the evidence still retained; a backfill must mark unavailable facts instead of inventing them. The current manager-loop escalation event is an attention signal, not an ending transition, so it must not create a terminal record. `Escalated` remains a reserved record disposition until the runtime exposes an explicit ending transition/event.

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

1. `subbundles/01-baseline-and-performance-characterization`
2. `subbundles/02-run-record-contracts-and-persistence`
3. `subbundles/03-terminal-summary-assembly-and-project-node`
4. `subbundles/04-optimized-history-detail-and-api-read-paths`
5. `subbundles/05-process-api-skill-parity`
6. `subbundles/06-performance-architecture-and-regression-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## UI Target Policy

- CanDoItAll applications target large-screen desktop use; do not add small/medium/mobile tuning unless explicitly requested.
- Reusable basic `CanDoItAll.Components.BaseLib` components remain responsible for small, medium, and large viewport behavior.

## Validation Summary

- Bundle preparation status: `Prepared; validator passed 2026-07-24`
- Execution status: `Completed; SB01-SB06 closed 2026-07-24`
- Subbundle gate review: `Pass; all six closure gates satisfied`
- Final closure gate: `Pass; independent C# architecture review and completed-stage validator passed`
- Browser validation analytics: `N/A; no Razor, CSS, component markup, layout, dialog, or scroll-owner change`
