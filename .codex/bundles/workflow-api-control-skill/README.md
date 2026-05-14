# Workflow API Control Skill

This bundle coordinates workflow API command parity, workflow API skill authoring, reinstall/setup proof, and closure.

## Profile

- `initiative`

## Mission

- Review the workflow HTTP API as the development control surface, close the missing workflow commands needed for definition lifecycle and portable authoring, add a repo-managed workflow API skill that mirrors the project-structure and processes API skills, and ensure the repo MCP reinstall script syncs the skill into the local Codex environment.

## Outcome Contract

- Requested outcome: workflow API commands cover development-time review, authoring, lifecycle, run control, observation, and human/external response flows; Codex has a `candoitall-api-workflows` skill installed locally after the MCP reinstall script runs.
- Hard constraints: preserve strongly typed workflow contracts, use focused HTTP endpoints instead of stringly command dispatch, do not reintroduce removed workflow-specific MCPs, and keep changes scoped to API, tests, skill docs, and install sync.
- Evidence required before closure: prepared-stage bundle validator, targeted workflow API tests, build or targeted test proof, skill metadata validation against current OpenAI skill docs, reinstall script proof, and local skill presence under `%USERPROFILE%\.codex\skills`.
- Known blockers or explicit scope exceptions: the OpenAI docs MCP was not exposed in this session and `codex.exe` from the Windows app package returned access denied, so OpenAI skill guidance is validated through official OpenAI web docs instead of the docs MCP.

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

1. `subbundles/01-workflow-api-gap-closure`
2. `subbundles/02-workflow-api-skill-and-reinstall-setup`
3. `subbundles/03-validation-and-environment-setup`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared and validated`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Subbundles 01-03 passed`
- Final closure gate: `Passed`
- Browser validation analytics: `N/A for API and skill work`
