# Architecture Review Checklist

## Source of Truth

- [ ] Raw sources are not replaced by summaries.
- [ ] Qdrant is documented as projection only.
- [ ] Every derived item has source refs.
- [ ] Source hash/change detection is defined.

## Human-Memory Analogy

- [ ] Episodic and semantic memory are separated.
- [ ] Procedural memory is separated from facts.
- [ ] Working memory is run/task-scoped.
- [ ] Consolidation is scheduled/idle and versioned.
- [ ] Recall is staged and explainable.
- [ ] Forgetting is soft: dormant/stale/superseded, not raw deletion.

## Existing Code Fit

- [ ] Module assembly registration fits current composition pattern.
- [ ] EF configuration discovery is used.
- [ ] Existing storage drivers are reused.
- [ ] Existing process/workflow records are used as sources.
- [ ] Existing RAG driver is wrapped, not duplicated.
- [ ] Existing semantic driver is wrapped, not duplicated.
- [ ] Existing workflow executor pattern is reused.
- [ ] Existing plugin capability model is reused.

## Retrieval Quality

- [ ] Semantic similarity is not treated as identity.
- [ ] Spatial/graph separation can override naive semantic merging.
- [ ] Context-separated relatedness is explicit.
- [ ] Recall intent changes candidate weighting.
- [ ] Recall traces support debugging.

## Security and Governance

- [ ] Secrets are classified/redacted before embedding.
- [ ] Access policy applies before context injection.
- [ ] High-risk procedures need review.
- [ ] Contradictions are preserved and reviewed.
- [ ] Generated summaries cannot become raw source truth.

## Distributed Compute

- [ ] Workers cannot mutate authoritative state.
- [ ] Job input/output hashes are enforced.
- [ ] Worker identities and capabilities are tracked.
- [ ] Coordinator validates before accepting output.

## UI/UX

- [ ] User can inspect why a memory was recalled.
- [ ] User can correct/split/merge memory.
- [ ] User can approve/reject review items.
- [ ] Consolidation run results are visible.
- [ ] Procedure memory can become actionable workflow/process work.
