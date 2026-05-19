# Project Structure Node Actions

This bundle is a coordination and execution package for `project-structure-node-actions`.

## Profile

- `feedback`

## Mission

- Make project-structure runtime, folder/file, repository, and link nodes useful from the canvas and useful to agents: valid runtime nodes can launch PowerShell normally or as admin, folders/files open the right local location, GitHub/GitLab links are recognized, and agent project-structure tools explain the correct node schemas.

## Outcome Contract

- Requested outcome: runtime, folder/file, repository/link, and agent catalog behavior matches the raw request without silently narrowing "all" runtime/file/folder language.
- Hard constraints: keep local path guards, keep PowerShell launch centralized, preserve existing managed asset/IPFS/process/workflow behavior, and validate with Playwright MCP screenshots.
- Evidence required before closure: targeted tests, browser screenshots, execution report rows, raw-note closure, and completed-stage bundle validation.
- Known blockers or explicit scope exceptions: UAC elevation click-through may need resolver/host-proof documentation if it cannot be automated safely.

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

1. `subbundles/01-01-runtime-launch-foundation`
2. `subbundles/02-02-folder-file-link-actions`
3. `subbundles/03-03-agent-catalog-and-ui-proof`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Ready`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed`
