# Project Structure MCP Initiative

This bundle is a coordination and execution package for `project-structure-mcp-bundle-1`.

## Profile

- `initiative`

## Mission

- Add a new CanDoItAll project-structure MCP that runs on each Codex workstation as a thin client against the main CanDoItAll machine, exposes filtered planning/reporting/editing access to projects and workbench nodes, enforces centrally managed agent policy and approval thresholds, prevents conflicting edits through shared leases, supports readonly asset retrieval and revisioned asset updates, provides project-management guidance for planning discussions, and ships with real end-to-end proof plus cross-machine setup material.

## Bundle Layout

- `inputs/` raw request, source notes, and structured input
- `analysis/` current-state audit, assumptions, and risk model
- `requirements/` normalized requirements with explicit scope
- `architecture/` target solution and boundaries
- `plan/` dependency map, sequence, and gates
- `traceability/` requirement and input coverage
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` self-review and execution proof
- `inventories/` impacted repo surfaces and rollout inventory
- `templates/` bundle-local reusable authoring material

## Recommended Execution Order

1. `C:\repositories\CanDoItAll\project-structure-mcp-bundle-1\subbundles\01-central-project-structure-agent-api-locking-checklist-import-and-analytics-foundation\README.md`
2. `C:\repositories\CanDoItAll\project-structure-mcp-bundle-1\subbundles\02-agent-policy-settings-and-knowledge-guidance-in-candoitall-web\README.md`
3. `C:\repositories\CanDoItAll\project-structure-mcp-bundle-1\subbundles\03-remote-project-structure-mcp-client-filters-and-cross-machine-setup\README.md`
4. `C:\repositories\CanDoItAll\project-structure-mcp-bundle-1\subbundles\04-real-end-to-end-validation-and-closure-audit\README.md`

## Dependency And Validation Map

- Keep the dependency graph, critical-subbundle notes, and progression gates current in `C:\repositories\CanDoItAll\project-structure-mcp-bundle-1\plan\01-phase-plan.md`.
- Re-run the prepared-stage validator after any material bundle repair.
- Do not start subbundle `03` until the policy and locking proof from `01` and `02` is trusted.
- Do not close the bundle until `04` records real chained node creation and readback proof, browser analytics for the settings UI, and raw-note closure for every source note.

## Validation Summary

- Bundle preparation status: `Completed`
- Bundle readiness gate: `Completed`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `Completed`
