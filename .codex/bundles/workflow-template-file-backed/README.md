# File-backed workflow templates

This bundle externalizes the default CanDoItAll workflow examples from compiled C# into durable YAML template files, with typed loading and seeding that preserves the current workflow model, validation rules, and runtime behavior.

## Profile

- `initiative`

## Mission

- Replace the compiled default workflow-template catalog in `WorkflowExampleCatalogSeedService` with a file-backed `Templates\Workflows` pack, modeled after MAF declarative YAML loading and the existing CanDoItAll process-template pack.

## Outcome Contract

- Requested outcome: all existing default workflow examples are represented as editable text files and seeded from those files.
- Hard constraints: no default workflow graph may remain compiled into `WorkflowExampleCatalogSeedService`; the runtime still consumes strongly typed CanDoItAll workflow models; YAML parse failures must fail loudly with path/key context.
- Evidence required before closure: prepared and completed bundle validators, focused unit tests proving template-pack loading and seeding, and a targeted build/test pass for the affected projects.
- Known blockers or explicit scope exceptions: this bundle creates a local file-backed catalog, not a hosted sharing marketplace or UI workflow catalogue.

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

1. `subbundles/01-workflow-template-pack-and-loader`
2. `subbundles/02-seed-service-conversion`
3. `subbundles/03-validation-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `N/A - backend/template storage change`
