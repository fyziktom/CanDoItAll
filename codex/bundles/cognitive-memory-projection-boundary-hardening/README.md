# Cognitive Memory Projection Boundary Hardening

This bundle is a prerequisite refactor package for the Cognitive Memory architecture. It hardens the projection-side boundaries in the existing RAG and SemanticCompletion repositories before Cognitive Memory depends on vector search, filtered recall, projection cleanup, or embedding profile hashes.

## Profile

- `initiative`

## Mission

- Add generic, provider-neutral projection controls to the RAG driver and stable embedding profile metadata to SemanticCompletion so Cognitive Memory can remain the source of truth while Qdrant/search remains a rebuildable projection.

## Outcome Contract

- Requested outcome: prepare implementation-ready work that adds typed RAG filtering, payload index support, delete-by-filter/source cleanup, projection lifecycle proof, and embedding profile metadata without putting Cognitive Memory-specific semantics into the generic driver repos.
- Hard constraints: do not implement Cognitive Memory; do not make Qdrant the canonical memory store; do not use stringly ad hoc filters in Cognitive Memory adapters; preserve existing RAG and SemanticCompletion behavior through additive contracts where practical.
- Evidence required before closure: targeted RAG driver tests, Qdrant mapper tests, SemanticCompletion embedding tests, sample/sandbox compile proof where affected, `dotnet test` proof in both related repos, and architecture sync back into `codex/bundles/cognitive-memory-architecture`.
- Known blockers or explicit scope exceptions: external Qdrant integration proof is optional when Qdrant is unavailable, but mapper-level and driver-contract tests are mandatory.

## Bundle Layout

- `inputs/` raw request, source artifacts, and structured input
- `analysis/` current state, assumptions, risks, and reopen triggers
- `requirements/` normalized requirements and acceptance criteria
- `architecture/` target projection boundary design
- `plan/` execution order, dependencies, critical foundations, and gates
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` self-review and execution report seed

## Recommended Reading Order

1. `inputs/00-original-request.md`
2. `analysis/01-current-state.md`
3. `analysis/02-assumptions-and-risks.md`
4. `requirements/01-normalized-requirements.md`
5. `architecture/01-target-solution.md`
6. `plan/01-phase-plan.md`
7. `subbundles/*/README.md`
8. `traceability/01-requirement-traceability.md`

## Recommended Execution Order

1. `subbundles/01-00-current-state-and-gate`
2. `subbundles/02-01-rag-filter-and-payload-contracts`
3. `subbundles/03-02-rag-projection-lifecycle`
4. `subbundles/04-03-semantic-embedding-profile`
5. `subbundles/05-04-validation-and-architecture-sync`

## Dependency And Validation Map

- Keep `plan/01-phase-plan.md`, subbundle statuses, and `reviews/01-execution-report.md` synchronized during implementation.
- This bundle blocks Cognitive Memory subbundles that use vector projection or projection-backed recall. It does not block pure module foundation or source snapshot ingestion that already consume the hardened source boundaries.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Not started`
- Subbundle gate review: `Seeded`
- Final closure gate: `Not started`
- Browser validation analytics: `N/A - no browser-visible or host-visible UI changes planned`
