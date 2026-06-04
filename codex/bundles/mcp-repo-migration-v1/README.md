# MCP Repository Migration

This bundle coordinates moving the CanDoItAll MCP server code out of the main application repository into `C:\repositories\CanDoItAll.Mcp`, while keeping workspace-specific settings, Codex skills, and resetup orchestration in the main `CanDoItAll` repository.

## Profile

- `initiative`

## Mission

Create a standalone MCP solution in `CanDoItAll.Mcp`, migrate the active MCP projects and MCP tests there, remove those projects from the main solution, update resetup tooling so it builds MCP binaries from the MCP repository while syncing skills from this repository, clean historical MCP artifacts from `.artifacts`, and document the new MCP repository.

## Outcome Contract

- Requested outcome: active MCP server projects build from `C:\repositories\CanDoItAll.Mcp`, not from `C:\repositories\CanDoItAll`.
- Hard constraints: keep Codex skills under `repo://codex/skills`; keep workspace settings under `repo://CanDoItAll.Mcp.*.settings.json`; do not reinstall suppressed `CanDoItAll.Mcp.Processes` or `CanDoItAll.Mcp.ProjectStructure`.
- Evidence required before closure: prepared bundle validation, standalone MCP solution build/test proof, resetup script proof, artifact cleanup proof, documentation proof, and final completed-stage validation.
- Known blockers or explicit scope exceptions: `tools/CanDoItAll.Manager` remains in the main application repository because it references `CanDoItAll.SharedKernel` and `CanDoItAll.Infrastructure`, so it is not an MCP server move candidate.

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
- `inventories/` source inventory and cleanup targets
- `templates/` reusable subbundle template

## Recommended Execution Order

1. `subbundles/01-01-mcp-solution-extraction`
2. `subbundles/02-02-reinstall-tooling-and-artifact-cleanup`
3. `subbundles/03-03-docs-and-final-validation`

## Dependency And Validation Map

- `SB01` owns the source migration and standalone solution. `SB02` must not start until the new solution builds.
- `SB02` owns resetup orchestration and artifact cleanup. `SB03` must not start until resetup has been exercised or an explicit host blocker is recorded.
- `SB03` owns docs, final verification, raw-note closure, and completed-stage bundle validation.

## Validation Summary

- Bundle preparation status: `Ready`
- Bundle readiness gate: `Prepared validator passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `N/A - repository/tooling migration`
