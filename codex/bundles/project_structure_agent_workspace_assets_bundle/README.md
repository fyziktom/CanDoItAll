# Project Structure Agent Workspace And Asset Tools

This bundle coordinates the changes needed for project-structure agents to work against selected external repositories, create Mermaid/file outputs as typed project-structure asset nodes, and use storage/file tools through explicit agent settings.

## Profile

- `initiative`

## Mission

Give technical agents a safe, settings-driven way to read/write selected external folders and storage catalogs while making ProjectStructure Mermaid and file outputs land in the correct typed node shapes.

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

1. `subbundles/01-external-workspace-selection`
2. `subbundles/02-project-structure-asset-output-contract`
3. `subbundles/03-storage-and-file-tool-defaults`
4. `subbundles/04-validation-and-closure`

## Dependency And Validation Map

- Dependency map, critical subbundles, and phase gates are in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared validator passed`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed; final bundle validator passed`
- Browser validation analytics: `Not run; UI change is a settings form section and compile/runtime tests cover the contract`
