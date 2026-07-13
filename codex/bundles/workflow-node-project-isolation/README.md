# Workflow Node Project Isolation

This initiative bundle prepares the long-run migration to isolate workflows, workflow nodes, workflow runtime services, and workflow executor families into dedicated projects while keeping MAF as a thin adapter and preserving plugin-provided executor behavior.

## Profile

- `initiative`

## Mission

Create implementation-ready workstreams for workflow project isolation: new workflow abstractions, builders and factories, core services, runtime/store boundaries, executor abstractions and category projects, plugin executor compatibility, template loading, MAF adapter reconnection, UI/API/workbench adoption, and forced refactoring-hardening checkpoints.

## Outcome Contract

- Requested outcome: original preparation-only request completed; current execution request supersedes it and implements the prepared subbundles in order.
- Hard constraints: build new abstractions and implementation projects from the base up before reconnecting MAF; isolate executors into their own abstractions/helpers and logical implementation categories; analyze plugin consequences deeply; keep behavior, executor ids, template keys, workflow definitions, runtime events, and process/workflow integrations compatible; preserve actionable structured failure diagnostics for workflow, executor, external tool/MCP, and plugin failures.
- Evidence required before closure: prepared-stage validator passes, every raw request is traced to one or more subbundles, current-state inventory cites real repo surfaces, the dependency plan gates adoption by prerequisite level, and the XLSX mapping workbook is generated and visually checked.
- Known blockers or explicit scope exceptions: no production implementation is included here; proposed project names are architecture targets and must be validated against actual MSBuild references during execution; final test command subsets may be adjusted by the implementation agent after project extraction.

## Bundle Layout

- `inputs/` raw request, source artifacts, and structured input.
- `analysis/` current-state findings, assumptions, risks, CodeAnalytics snapshot, and performance review.
- `requirements/` normalized, testable requirements and hard constraints.
- `architecture/` target solution, project graph, executor boundaries, failure-diagnostic boundaries, plugin consequences, and quality gates.
- `inventories/` scope, source, executor, plugin, error-state, and test inventories.
- `templates/` proposed template or worksheet references for repeatable execution.
- `plan/` dependency-aware subbundle order, critical foundations, and phase gates.
- `traceability/` requirement-to-subbundle and proof mapping.
- `shared-prompts/` implementation and QA prompts.
- `subbundles/` numbered execution-ready workstreams.
- `reviews/` self-review and execution report seed.
- `proof/` expected future proof structure for critical subbundles.

## Recommended Execution Order

1. `subbundles/01-workflow-boundary-inventory-and-project-graph`
2. `subbundles/02-workflow-abstractions-and-builders-foundation`
3. `subbundles/03-workflow-core-services-extraction`
4. `subbundles/04-workflow-runtime-and-store-abstractions`
5. `subbundles/05-foundation-refactoring-hardening-checkpoint`
6. `subbundles/06-executor-abstractions-and-shared-helpers`
7. `subbundles/07-default-executor-category-projects`
8. `subbundles/08-plugin-executor-boundary-and-adapters`
9. `subbundles/09-executor-refactoring-hardening-checkpoint`
10. `subbundles/10-workflow-template-and-descriptor-loading`
11. `subbundles/11-maf-compiler-backend-adapter-isolation`
12. `subbundles/12-api-ui-workbench-adoption`
13. `subbundles/13-adoption-refactoring-hardening-checkpoint`
14. `subbundles/14-regression-proof-cleanup-and-docs`

## Dependency And Validation Map

- Keep `plan/01-phase-plan.md` current. It contains the subbundle dependency map, critical subbundles, and phase gates.
- If the bundle resumes after compaction or handoff, use this README, the current subbundle README, the XLSX mapping, and `reviews/01-execution-report.md` as durable state.
- Critical subbundles require Semantic Adequacy Gate proof and artifact-backed manifests under `proof/SBxx/` before dependent subbundles can proceed.
- Checkpoint subbundles SB05, SB09, and SB13 are mandatory refactoring-hardening gates. They must run focused architecture, dependency, diagnostics, no-generic-error, file-size/responsibility, and performance reviews before downstream adoption.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `SB01, SB02, SB03, SB04, SB05, SB06, SB07, SB08, SB09, SB10, SB11, SB12, SB13, and SB14 completed`
- Final closure gate: `Completed-stage validator passed after SB14 metadata repair`
- Browser validation analytics: `SB12, SB13, and SB14 large-screen workflow shell and Workbench workflow-node browser proof passed; small and medium viewport tests skipped per user instruction`
- CodeAnalytics snapshot: `snap-20260629143729-e43d210b`
- XLSX mapping: `bundle://inventories/workflow-node-project-isolation-map.xlsx` updated through SB14
