# Assumptions And Risks

## Assumptions

- RAG and SemanticCompletion remain separate reusable repositories.
- Cognitive Memory will consume them through adapters and must not become embedded inside either repo.
- Qdrant may be unavailable in normal local test runs, so provider-neutral contract tests and Qdrant mapper tests must carry most proof.
- Existing RAG consumers should keep working after additive contract changes.
- Existing SemanticCompletion consumers should keep working after embedding metadata is added.

## Critical Path Risks

- `02-01-rag-filter-and-payload-contracts` is a critical foundation. If filter shape is weak or stringly, every downstream projection and recall adapter will copy that weakness.
- `03-02-rag-projection-lifecycle` is a critical foundation. Without delete-by-filter/source and payload index operations, projection rebuilds and stale cleanup will become ad hoc bookkeeping in Cognitive Memory.
- `04-03-semantic-embedding-profile` is a critical foundation for projection correctness. If embedding profiles are not stable, projection hashes and rebuild decisions cannot be trusted.

## Validation Risks

- Live Qdrant integration tests may be blocked by environment availability. Mapper-level translation tests are required regardless.
- Untyped metadata values can still carry provider-specific constraints. The bundle should add typed filter contracts without overdesigning a full query language.
- Public API changes can break samples or consumers if constructor signatures change. Prefer additive init-only properties or overloads where possible.
- Semantic profile ids must be deterministic across processes and machines; paths that include absolute local directories should not become the whole profile identity.

## Reopen Triggers

- Reopen RAG filter contracts if Cognitive Memory adapters need to post-filter unscoped vector results.
- Reopen RAG lifecycle if stale projections cannot be deleted by source scope, projection version, or embedding profile without enumerating all point ids.
- Reopen SemanticCompletion profile if projection records cannot record provider/model/dimension/normalization/profile in a stable way.
- Reopen architecture sync if `cognitive-memory-architecture` still suggests starting projection-backed recall before this bundle closes.
- Reopen validation if only compile proof exists and no mapper/unit tests cover filter, delete, payload index, and profile behavior.
