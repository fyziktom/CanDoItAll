# MCP Reinstall Template-Free Shadow Build

This bundle is a coordination and execution package for `mcp-reinstall-template-free-shadow-build`.

## Profile

- `feedback`

## Mission

- Make MCP reinstallation build only the MCP outputs it needs, without copying repository `Templates` into MCP install or DotNetWatch shadow artifacts. The reinstall script should keep syncing repo-managed skills and should validate through the same `tools\Reinstall-CanDoItAllMcps.ps1` path that failed in the console output.

## Outcome Contract

- Requested outcome: `tools\Reinstall-CanDoItAllMcps.ps1` prepares and installs MCP servers without failing on long paths under `Templates\Agents`.
- Hard constraints: repository templates stay in `Templates`; MCP-related builds do not require them; skills sync remains part of reinstall; DotNetWatch still launches from an artifact-backed shadow copy rather than directly from a locked repo `bin` path.
- Evidence required before closure: failing-first transcript of the current shadow-build failure or equivalent current-state proof, passing transcript from `tools\Reinstall-CanDoItAllMcps.ps1`, source assertions that MCP artifact paths do not contain copied `Templates`, changed-file hashes, anti-stub audit, and a closure verifier.
- Known blockers or explicit scope exceptions: no UI or browser proof is required because the work is host/build-script behavior only.

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

1. `subbundles/01-mcp-reinstall-build-pipeline-and-proof`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Ready`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `N/A - host/build-script work only`
