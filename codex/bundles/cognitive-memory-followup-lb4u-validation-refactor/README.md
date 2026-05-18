# Cognitive Memory Follow-Up LB4U Validation And Refactor

This bundle is the execution package for finishing the cognitive memory v2 work after the original architecture bundle was closed.

## Profile

- `initiative`

## Mission

Finish cognitive memory as a useful, observable project-memory system instead of only a broad API surface. The work must use the original v2 bundle as the contract, validate the real implementation against staged LB4U project data, improve weak ingestion/consolidation/recall loops, and leave the codebase smaller and easier to maintain where the current implementation has grown into oversized services.

## Outcome Contract

- Requested outcome: a validated cognitive memory implementation that can ingest realistic staged project knowledge, preserve raw provenance, produce useful canonical memories and cross-project knowledge, answer probing questions with traceable context, and pass OpenAI plus local Ollama validation.
- Hard constraints: read `C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U` as read-only; do not ingest password/secret files; do not mutate canonical truth directly from probes; do not treat generated summaries as raw truth; do not add fallback behavior that silently hides model/provider/configuration failures.
- Evidence required before closure: prepared and completed bundle validation, subbundle gate proof, automated tests, API smoke proof, OpenAI `gpt-5-mini` multi-cycle validation, Ollama `gptoss20b64k` validation with explicit output token proof, probing transcript summaries, memory snapshot/probe evidence, and updated API/skill/docs if the surface changes.
- Known blockers or explicit scope exceptions: this prepared bundle does not execute implementation changes; it defines the follow-up workstreams and validation gate. Execution must start from `subbundles/00-reentry-and-harness-gate`.

## Bundle Layout

- `inputs/` raw request, source artifacts, structured staged LB4U input, and probe scripts.
- `analysis/` original contract audit, implementation audit, gap analysis, and risks.
- `requirements/` normalized requirements and acceptance criteria.
- `architecture/` target follow-up shape and refactor boundaries.
- `plan/` execution order, dependency map, and gates.
- `traceability/` requirement, input, and validation coverage.
- `shared-prompts/` reusable implementation, QA, and probing prompts.
- `subbundles/` numbered execution-ready workstreams.
- `inventories/` current source and LB4U asset inventories.
- `templates/` staged ingestion and probe session templates.
- `checklists/` workbook control artifact and generation script.
- `reviews/` self-review and execution report skeleton.

## Recommended Execution Order

1. `subbundles/00-reentry-and-harness-gate`
2. `subbundles/01-implementation-audit-refactor-map`
3. `subbundles/02-lb4u-staged-inputs-secret-safety`
4. `subbundles/03-model-profile-token-settings`
5. `subbundles/04-model-assisted-consolidation`
6. `subbundles/05-epistemic-cross-project-knowledge`
7. `subbundles/06-probing-feedback-regression-loop`
8. `subbundles/07-maintainability-file-splits`
9. `subbundles/08-openai-lb4u-validation-cycle`
10. `subbundles/09-ollama-gptoss20b64k-validation`
11. `subbundles/10-api-skill-docs-closure`

## Dependency And Validation Map

- Keep `plan/01-phase-plan.md`, `traceability/01-requirement-traceability.md`, and `reviews/01-execution-report.md` current after every subbundle.
- Treat the workbook `checklists/cognitive-memory-followup-control.xlsx` as the human gate tracker for execution status, evidence links, and reopen triggers.
- If the bundle resumes after compaction, read this README, the active subbundle README, `inputs/00-original-request.md`, and `reviews/01-execution-report.md` before editing code.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Ready for implementation`
- Subbundle gate review: `Prepared`
- Final closure gate: `Pending implementation`
- Browser validation analytics: `Pending UI/API execution`
- Bundle readiness gate: `Prepared-stage validator passed on 2026-05-18`
