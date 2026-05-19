# Qdrant Beta Validation Source

## Projection Ready Fact

The Cognitive Memory beta validation source says that P1 beta requires Docker Qdrant projection proof. The proof record must mention deterministic local hashing embeddings, the `candoitall-knowledge` collection, and the `qdrant-default-v1` projection profile. It also states that vector recall must use the public recall API and must not silently skip the vector projection stage.

## Recall Check

For the recall validation question, the expected source-backed answer should include Docker Qdrant, the `local-hashing-v1:dimension=384` embedding profile, and a successful vector projection stage. This source exists only to validate the beta path for Cognitive Memory projection and recall.
