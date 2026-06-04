# API Docs Skills Parity v1

This bundle is the implementation plan and evidence map for repairing drift between the current CanDoItAll APIs, API skills, and docs after the recent agents, providers, workflows, processes, project-structure, and cognitive-memory work.

## Profile

- `initiative`

## Mission

- Build a source-backed API, DTO, docs, skills, and agent-tool parity map, then execute repairs in dependency order so the public docs, repo-managed skills, active local skills, API contracts, and validation guardrails all match the current code.

## Outcome Contract

- Requested outcome: detailed analysis of missing, obsolete, and stale API/docs/skill coverage, captured in an XLSX workbook and converted into an executable step-by-step repair plan.
- Hard constraints: preserve the raw request; use source inspection instead of memory; keep changes small per phase; do not silently hide API drift; update active skill copies after repo skill edits; record validation proof before closure.
- Evidence required before closure: regenerated XLSX gap map, route/DTO/tool coverage proof, updated docs/skills, focused API and tool tests, skill hash sync proof, and completed bundle validator output.
- Known blockers or explicit scope exceptions: process and project-structure runtime tool gaps were resolved as explicit HTTP-only boundaries rather than broad direct-tool additions.

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

1. `subbundles/01-source-of-truth-api-and-dto-inventory`
2. `subbundles/02-http-api-contract-repairs`
3. `subbundles/03-agent-tool-surface-parity`
4. `subbundles/04-documentation-refresh`
5. `subbundles/05-api-skills-refresh-and-active-skill-sync`
6. `subbundles/06-validation-harness-and-drift-guardrails`
7. `subbundles/07-final-closure-and-handoff`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.
- The canonical planning workbook is `inventories/api-docs-skills-gap-map.xlsx`; the builder used for this preparation is preserved at `inventories/build-gap-map.mjs`, with the runnable current workspace copy at `.codex/tmp/api-docs-skills-gap-map/build-gap-map.mjs`.

## Validation Summary

- Bundle preparation status: `Completed`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `N/A; no UI-affecting changes`
