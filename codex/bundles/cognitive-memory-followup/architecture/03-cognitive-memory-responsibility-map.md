# Cognitive Memory Responsibility Map

## Purpose

SB09 keeps the SB04-SB08 behavior but makes the owned algorithm seams explicit enough to test and version. The intent is to prevent future changes from silently editing one large service method and missing persistence, provenance, or lifecycle invariants.

## Versioned Algorithm Configuration

- `CognitiveMemoryQualityAlgorithmOptions` is the single owned configuration root for quality memory algorithms.
- Cluster, dream, aggregate apply, professor lifecycle, and recall settings each carry a version label or explicit threshold values.
- Persisted records still store the algorithm version used by the owning service.

## Clustering

- `CognitiveMemoryClusterPlanner` owns EF orchestration, persisted cluster records, run status, and quality metrics.
- `ICognitiveMemoryClusterKeyExtractor` owns deterministic text/key extraction.
- `ICognitiveMemoryCandidatePairSelector` owns candidate fanout, semantic fallback, and pair selection.
- `ICognitiveMemoryClusterSemanticSimilarityProvider` owns lightweight alias-aware similarity checks.

## Dreaming

- `CognitiveMemoryDreamConsolidationService` owns dream run orchestration, persistence, aggregate candidate records, and source maps.
- `ICognitiveMemoryDreamClaimSynthesizer` owns claim text synthesis from grouped source claims.
- `CognitiveMemoryDreamValidator` owns validation orchestration and review routing.
- `ICognitiveMemoryDreamEntailmentValidator` owns source entailment checks used by validation.
- `CognitiveMemoryAggregateConfidenceCalibrator` and `CognitiveMemoryAggregateMemoryApplicator` continue to own confidence calibration and aggregate promotion.

## Professor Lifecycle

- `ICognitiveMemoryProfessorTeachingExtractor` owns natural-language professor anchor extraction from curator turns.
- `ICognitiveMemoryProfessorAssimilationEvaluator` owns assimilation criteria, direct-self-proof rejection, repeated-use checks, and descendant traversal depth.
- `CognitiveMemoryProfessorAnchorService` owns anchor persistence, state transitions, and fading of direct capture memory.

## Recall

- `CognitiveMemoryRecallSynthesisService` owns EF read/write orchestration for synthesized recall records and source-map persistence.
- `ICognitiveMemoryRecallBriefComposer` owns query-shaped brief composition, caveat/conflict statement shaping, and per-statement aggregate claim lineage.
- `CognitiveMemoryReferenceResolver` owns on-demand expansion from synthesized statements back to allowed source references.

## Validation Ownership

- Direct collaborator tests cover pure collaborators without EF where possible.
- Module registration tests prove the collaborators and options are registered through the cognitive-memory module.
- Broad SB04-SB08 regression tests remain the behavioral guardrail for the refactor.
