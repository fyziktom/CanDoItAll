# MCP Idle Shutdown

This bundle is a coordination and execution package for `mcp-idle-shutdown`.

## Profile

- `feedback`

## Mission

- Components MCP and SSH Ops MCP must stop accumulating idle stdio server processes. Both servers should shut themselves down after a configurable inactivity window, with a short default for the documentation-style Components MCP and a longer default for the stateful SSH Ops MCP.

## Outcome Contract

- Requested outcome: add an explicit idle shutdown policy to `CanDoItAll.Mcp.Components` and `CanDoItAll.Mcp.SshOps`.
- Hard constraints: keep the lifecycle explicit and observable, do not hide errors, keep the change small, and share common lifetime behavior through `CanDoItAll.Mcp.Core`.
- Evidence required before closure: bundle prepared and completed validators pass, targeted tests pass, and the MCP projects build.
- Known blockers or explicit scope exceptions: no browser proof is required because this is stdio host lifecycle behavior, not browser-visible UI.

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

1. `subbundles/01-shared-idle-shutdown`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Ready`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `N/A`
