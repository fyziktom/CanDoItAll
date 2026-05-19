# Cognitive Memory Quality Foundation Hardening Follow-up

This bundle is a coordination and execution package for hardening the implementation introduced by commit `228737d90acad18d96b9673949cdb5bd785f3fc6` (`phase1`) for `cognitive-memory-quality-foundation-dreaming-synthesis`.

The prior bundle says all seven phases are complete and its structural validator passes. This follow-up does not accept that as sufficient proof. The implementation added the expected contracts, persistence tables, and happy-path tests, but it still needs production hardening around idempotency, repeat execution, transactional failure handling, mode-specific dream behavior, aggregate provenance quality, recall synthesis semantics, and a broader regression corpus.

## Profile

- `initiative`

## Mission

Make the cognitive memory quality foundation durable enough to execute repeatedly in real systems: cluster planning must be idempotent, dream runs must have explicit lifecycle and failure semantics, aggregate memories must remain grounded and policy-safe, recall synthesis must produce a real concise brief with resolvable references, and the test corpus must prove adversarial and repeat-run behavior rather than only first-run happy paths.

## Outcome Contract

- Requested outcome: implementation-ready hardening and validation work that finishes the quality foundation beyond the phase-one scaffold.
- Hard constraints: preserve existing public contracts unless a change is explicitly justified; keep cognitive-memory boundaries inside `CanDoItAll.Modules.CognitiveMemory`; do not add economic memory governance; do not hide unsupported dream modes behind default behavior.
- Evidence required before closure: failing-before/fixed-after regression tests for repeat cluster planning, repeat dream runs, failure handling, mode policies, redaction, aggregate apply idempotency, reference safety, plus clean targeted and full CognitiveMemory test runs.
- Known blockers or explicit scope exceptions: no Blazor route was changed in the last commit, so browser validation is not required unless implementation adds UI surfaces; semantic/LLM synthesis may use deterministic fakes for tests if provider integration is not available.

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

1. `subbundles/01-01-reentry-audit-and-regression-safety-net`
2. `subbundles/02-02-cluster-planner-idempotency-and-source-substrate`
3. `subbundles/03-03-dream-run-lifecycle-and-mode-policies`
4. `subbundles/04-04-aggregate-provenance-validation-and-application`
5. `subbundles/05-05-recall-synthesis-and-reference-safety`
6. `subbundles/06-06-persistence-diagnostics-and-service-refactor`
7. `subbundles/07-07-end-to-end-quality-corpus-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Ready`
- Bundle readiness gate: `Passed prepared-stage structural validation`
- Execution status: `Not started`
- Subbundle gate review: `Not started`
- Final closure gate: `Not started`
- Browser validation analytics: `Not applicable unless implementation adds UI`
