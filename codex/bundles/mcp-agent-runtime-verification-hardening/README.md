# MCP Agent Runtime Verification Hardening

This bundle records the verification and hardening work for the Agents capability setup failure reported on the Playwright Local MCP capability, plus follow-on proof that project-structure, workflow, and process agent tooling still works after the capability/runtime changes.

## Profile

- `feedback`

## Mission

- Repair live MCP setup validation so Playwright Local MCP can be started and inspected from the Agents capability details dialog, update stale development workspace records to the current capability model, and prove representative agents still receive governed project-structure/process/workflow tool access through the internal runtime path.

## Outcome Contract

- Requested outcome: Playwright Local MCP setup passes in the live app on port 5032, development DB records contain the current model fields, and agent runtime access to project/process/workflow tooling is validated.
- Hard constraints: use the existing C#/.NET and Blazor architecture, keep UI validation to large screens only, avoid replacing internal project/process tools with legacy MCP records, and keep the app running on port 5032 with the development workspace.
- Evidence required before closure: focused .NET test results, live development workspace inspection, Playwright MCP UI proof, and large-screen screenshots for related pages.
- Known blockers or explicit scope exceptions: no small or medium viewport testing was performed by request.

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

1. `subbundles/01-mcp-setup-runtime-repair`
2. `subbundles/02-database-catalog-compatibility`
3. `subbundles/03-agent-process-workflow-tool-verification`
4. `subbundles/04-hardening-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed at 1920x1080`
