# Workflow Basic Routing MAF

## Profile

- `initiative`

## Mission

- Add first-class basic routing to CanDoItAll workflow definitions and the workflow canvas using the Microsoft Agent Framework workflow primitives that already exist today.
- Support deterministic `IF/ELSE`, `SWITCH/default`, and multi-selection fan-out routing now, while keeping a clean replacement seam for the later ARTL DSL.
- Preserve the existing workflow executor, LLM component, persistence, process-integration, and canvas boundaries.
- Reopened 2026-05-11 to improve production workflow authoring: clean PostgreSQL datasource setup, richer decision-node canvas UX, setup-dialog renderer coverage, and practical seeded workflow examples that exercise IF/ELSE, SWITCH/default, and fan-out logic.

## Outcome Contract

- Requested outcome: workflow authors can add conditional and switch-style branches in the workflow canvas, save them in the workflow definition, validate them, and run in-process MAF preview execution where routes are honored by MAF `AddEdge<T>`, `AddSwitch`, and `AddFanOutEdge<T>` instead of being interpreted in a higher layer.
- Hard constraints: use MAF's built-in routing APIs for this phase; do not implement ARTL now; do not evaluate arbitrary C# or user-supplied script; keep routing contracts strongly typed and serializable; maintain backward compatibility for existing saved `ConditionExpression` values; avoid silent fallback from invalid route definitions.
- Evidence required before closure: unit tests for route serialization/evaluation/compiler grouping, runtime tests proving only expected branches execute, component tests for workflow canvas routing authoring, API/persistence compatibility tests, and browser proof for canvas route creation/editing/preview.
- Known blockers or explicit scope exceptions: production DurableTask/DTS routing proof remains outside this bundle unless the current host already supports it; ARTL syntax/parser is intentionally deferred and represented only by a stable compiler seam.

## Current MAF Reference Baseline

- The current Microsoft repository exposes workflow samples under `dotnet/samples/03-workflows`, including `ConditionalEdges` with `01_EdgeCondition`, `02_SwitchCase`, and `03_MultiSelection`.
- Microsoft Agent Framework API documentation for `WorkflowBuilder.AddEdge` includes generic overloads with `Func<T?, bool>` route predicates and a non-generic string overload that is a visualization label, not a predicate.
- Microsoft Agent Framework API documentation for `WorkflowBuilderExtensions.AddSwitch` describes switch-style conditional branching from a source executor.
- Microsoft Agent Framework API documentation for `WorkflowBuilder.AddFanOutEdge` includes generic overloads with target selector delegates for multi-selection routing.

## Bundle Layout

- `inputs/` raw request, source artifacts, and normalized structured input.
- `analysis/` current repo state, package/source observations, assumptions, risks, and reopen triggers.
- `requirements/` normalized, testable requirements and input coverage.
- `architecture/` target contracts, compiler boundary, UI authoring model, and ARTL handoff seam.
- `inventories/` exact source inventory and expected ownership.
- `plan/` dependency-aware phase plan and gates.
- `traceability/` requirement-to-bundle mapping.
- `shared-prompts/` reusable implementation and QA prompts.
- `subbundles/` numbered execution-ready workstreams.
- `templates/` proposal snippets and checklists for implementation agents.
- `reviews/` self-review and execution-report seed.

## Recommended Execution Order

1. `subbundles/01-routing-domain-contracts-and-compatibility`
2. `subbundles/02-maf-compiler-routing-integration`
3. `subbundles/03-workflow-canvas-routing-authoring-ux`
4. `subbundles/04-validation-persistence-api-and-scenario-seeds`
5. `subbundles/05-routing-test-proof-browser-proof-and-artl-handoff`
6. `subbundles/06-postgresql-clean-test-datasource`
7. `subbundles/07-decision-node-canvas-ux-and-setup-renderers`
8. `subbundles/08-production-example-workflows-and-llm-tuning`
9. `subbundles/09-execution-observation-repair-and-final-proof`

## Dependency And Validation Map

- Subbundle 01 is the critical domain foundation. Do not start compiler work until route contracts serialize cleanly and existing definitions remain readable.
- Subbundle 02 is the runtime foundation. Do not start UI closure before runtime tests prove predicates, switch defaults, and fan-out target selection work against `WorkflowNodeInput.PayloadJson`.
- Subbundle 03 is the browser-visible foundation. Do not close the bundle without Playwright/browser proof that canvas-authored routes can be created, edited, saved, and preview-run.
- Subbundle 04 may run in parallel with UI styling only after the domain model is stable, but its persistence/API compatibility tests must pass before final proof.
- Subbundle 05 closes the raw request, reviews proof, and records the ARTL handoff contract.
- Subbundle 06 adds a clean PostgreSQL datasource/profile for Visual Studio verification and must not drop any database except the explicitly named workflow-routing test database.
- Subbundle 07 improves the workflow canvas decision-block shape, right-click second-layer menu, toolbox presence, and first-create setup dialogs through renderer-keyed actions.
- Subbundle 08 seeds practical workflows and tuned component/executor settings for document, email, spreadsheet, internet-fetch, and additional production-like scenarios.
- Subbundle 09 runs and observes at least 20 real-world workflow scenarios, records failures, repairs implementation or bundle notes, and captures browser screenshots.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed by local prepared-stage validator`
- Execution status: `Completed follow-up implementation`
- Subbundle gate review: `Subbundles 01-09 completed`
- Final closure gate: `Passed follow-up validation`
- Browser validation analytics: `Decision-node visual proof, nested menu proof, decision setup dialog proof, and executor setup dialog proof captured`
