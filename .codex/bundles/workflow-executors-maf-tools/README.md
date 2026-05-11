# Workflow Executors MAF Tools

## Profile

- `initiative`

## Mission

- Add first-class executable workflow nodes for MAF-backed workflows: file/storage access, project-structure reads and asset creation, HTTP/HTTPS fetches, AI image generation, and spreadsheet read/write through a new `CanDoItAll.Tools.Documents` wrapper around ClosedXML.
- Keep the architecture plugin-ready without implementing the full plugin system in this phase. Executor contracts must describe execution, settings schema, default policy, catalog grouping, and future UI setup renderer keys.
- Make workflow canvas authoring expose executors through a second-level right-click menu and a component toolbox similar to the project-structure canvas.
- Reopened follow-up: move workflow toolbox and selection into canvas floating windows, require modal creation and double-click details/editing, split the workflows page into operational tabs, add observer-grade workflow APIs, and prove 20 real-world examples in a PostgreSQL-backed testing instance with seeded projects/project structures.
- Multi-step follow-up: prove executor outputs can flow through real LLM calls and into downstream executors, including project-structure read -> LLM transform -> project-structure asset save.

## Outcome Contract

- Requested outcome: workflow definitions can include typed executor nodes that run real tools during in-process MAF preview execution and can later be hosted by durable MAF runners without changing saved definitions.
- Hard constraints: use strongly typed executor ids and settings contracts; no stringly typed dispatch; no silent fallback; preserve UI/Core/MAF/document-library boundaries; use ClosedXML only behind `CanDoItAll.Tools.Documents`; keep plugin integration as explicit contracts, not a half-integrated plugin runtime.
- Evidence required before closure: build/test proof, 20 real workflow scenario results, one gpt-5-mini provider attempt, one `gptoss20b64k` Ollama attempt, workbook plan artifact, browser proof for toolbox/right-click behavior, and architecture review notes covering plugin boundary and non-happy-path policy.
- Known blockers or explicit scope exceptions: production DurableTask/DTS hosting remains outside this bundle unless the existing app already supplies the host; custom plugin loading and custom plugin-rendered setup components are prepared by interfaces only.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input.
- `analysis/` current state, assumptions, risks, and source review.
- `requirements/` normalized, testable requirements and input coverage.
- `architecture/` target contracts, executor model, and plugin boundary.
- `plan/` dependency-aware subbundle order and gates.
- `traceability/` requirement-to-subbundle mapping.
- `shared-prompts/` reusable implementation and QA prompts.
- `subbundles/` numbered execution-ready workstreams.
- `reviews/` self-review and live execution report.
- `artifacts/` xlsx plan and validation evidence.

## Recommended Execution Order

1. `subbundles/01-executor-contracts-catalog-and-plugin-architecture`
2. `subbundles/02-documents-wrapper-and-spreadsheet-executor`
3. `subbundles/03-workspace-http-image-and-project-structure-executors`
4. `subbundles/04-maf-compiler-runtime-policy-and-artifacts`
5. `subbundles/05-workflow-canvas-toolbox-and-node-setup-ui`
6. `subbundles/06-workflow-scenario-validation-and-provider-tests`
7. `subbundles/07-architecture-review-closure-and-followups`
8. `subbundles/08-workflow-canvas-floating-windows-modals-and-tabs`
9. `subbundles/09-workflow-control-apis-and-observer-contract`
10. `subbundles/10-postgresql-test-db-projects-and-realworld-scenarios`
11. `subbundles/11-final-browser-scenario-closure`

## Dependency And Validation Map

- Keep `plan/01-phase-plan.md` current and rerun prepared-stage validation when implementation reality changes scope.
- Treat subbundles 01, 02, and 04 as critical foundations. If any of their assumptions fail, stop and repair the bundle before moving downstream.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed by script`
- Execution status: `Completed`
- Subbundle gate review: `Executed; see reviews/01-execution-report.md`
- Final closure gate: `Passed by completed-stage validator`
- Browser validation analytics: `Playwright proof captured for tabs, floating windows, create modal, and double-click details modal`
- Multi-step scenario proof: `25 PostgreSQL-backed scenarios completed, including 3 executor -> gpt-5-mini -> executor transfer chains`
