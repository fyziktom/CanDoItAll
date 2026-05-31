# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| Raw request preserved. | `inputs/00-original-request.md` | SB01 | Prepared validator plus raw note closure table. | Must remain verbatim. |
| Detailed missing/obsolete/API/DTO/docs/skills analysis. | `analysis/01-current-state.md`, `inventories/api-docs-skills-gap-map.xlsx` | SB01 | Regenerated XLSX and source counts. | Workbook is the primary map. |
| API contract and route exposure repairs. | `requirements/01-normalized-requirements.md#rq-002` | SB02 | Focused OpenAPI/API route tests. | Cognitive Memory routes are highest risk. |
| Agent tool call parity. | `inventories/api-docs-skills-gap-map.xlsx` Tool Parity sheet | SB03 | Runtime tool/policy tests or explicit HTTP-only exception. | Blocks skills claims. |
| Docs refresh. | `requirements/01-normalized-requirements.md#rq-004` | SB04 | Markdown diff review, route/DTO source assertions. | Includes historical docs. |
| API skills refresh. | `requirements/01-normalized-requirements.md#rq-005` | SB05 | Route/DTO skill appendix review and hash sync proof. | Active local sync required. |
| Drift guardrails. | `requirements/01-normalized-requirements.md#rq-006` | SB06 | New or updated parity test/script output. | Must protect future changes. |
| Step-by-step long-task plan. | `plan/01-phase-plan.md`, `subbundles/*/README.md` | All | Subbundle gates and execution report. | Durable state prevents losing context. |
