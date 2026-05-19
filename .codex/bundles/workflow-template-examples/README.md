# Workflow Template Examples

This bundle is a coordination and execution package for `workflow-template-examples`.

## Profile

- `initiative`

## Mission

- Add a small set of workflow template examples to the agents workflow module using repository-owned template files loaded by the existing workflow template pack loader. The examples must cover Gmail and Office365 email summaries, email-to-task extraction into a specified project structure node, Mermaid graph generation from an input file, and source-code file summaries.

## Outcome Contract

- Requested outcome: workflow examples are available through the template pack seed path and no example workflow graph is hard-coded in C#.
- Hard constraints: keep templates in `C:\repositories\CanDoItAll\Templates\Workflows`; load them through `manifest.yaml`; preserve existing examples; do not overwrite user-managed workflow definitions.
- Evidence required before closure: prepared-stage bundle validation, template loader validation, targeted unit test coverage, and a build/test command covering the workflow template pack.
- Known blockers or explicit scope exceptions: live Gmail/Office365 execution requires installed/enabled OAuth plugin connections and is not expected as local proof for this template-only change.

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

1. `subbundles/01-template-pack-file-loading-foundation`
2. `subbundles/02-email-plugin-workflow-examples`
3. `subbundles/03-file-analysis-workflow-examples`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Implemented`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `N/A - template/data pack and unit-test proof`
