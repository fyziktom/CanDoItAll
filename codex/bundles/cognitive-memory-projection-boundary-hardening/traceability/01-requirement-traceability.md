# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
|---|---|---|---|---|
| Post-hardening review must confirm existing boundary work is complete. | `analysis/01-current-state.md` | `subbundles/01-00-current-state-and-gate` | Re-run targeted CanDoItAll tests or cite fresh proof; update execution report. | Do not redo source boundary implementation. |
| PR-002 typed RAG filters. | `requirements/01-normalized-requirements.md` | `subbundles/02-01-rag-filter-and-payload-contracts` | RAG model validation tests and Qdrant mapper tests. | Must stay provider-neutral. |
| PR-003 payload indexes. | `requirements/01-normalized-requirements.md` | `subbundles/02-01-rag-filter-and-payload-contracts` | RAG capability/index tests or explicit unsupported behavior tests. | Required for high-volume scoped search. |
| PR-004 projection lifecycle cleanup. | `architecture/01-target-solution.md` | `subbundles/03-02-rag-projection-lifecycle` | Delete-by-filter/source tests and stale cleanup scenario. | Avoid direct Qdrant calls from Cognitive Memory. |
| PR-005 capability discovery. | `requirements/01-normalized-requirements.md` | `subbundles/02-01-rag-filter-and-payload-contracts` and `subbundles/03-02-rag-projection-lifecycle` | Unsupported capability tests. | No silent filter/index/delete ignoring. |
| PR-006 embedding profile metadata. | `architecture/01-target-solution.md` | `subbundles/04-03-semantic-embedding-profile` | SemanticCompletion embedding tests. | Profile must be stable enough for projection hashes. |
| PR-007 keep generic repos generic. | `requirements/01-normalized-requirements.md` | all implementation subbundles | Source review with `rg` for Cognitive Memory-specific naming in RAG/SemanticCompletion. | Cognitive Memory fields can be payload metadata, not driver model semantics. |
| PR-008 architecture sync. | `plan/01-phase-plan.md` | `subbundles/05-04-validation-and-architecture-sync` | Updated `cognitive-memory-architecture` docs and prepared/completed validation. | Blocks projection-backed recall phases only. |
