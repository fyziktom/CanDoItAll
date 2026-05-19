# Cognitive Memory Quality Foundation, Dreaming, Clustering, and Synthesis

This initiative bundle prepares Codex to upgrade the Cognitive Memory module from a mostly linear ingestion/consolidation/recall pipeline into a quality-oriented memory system that can cluster related memories, run explicit dreaming passes, produce validated aggregate memories, and synthesize concise consumer-facing memory briefs with drill-down provenance.

The bundle intentionally excludes memory economics, attention markets, pricing, lending, or budget governance models. Those should be added only after the base memory quality, aggregation, validation, and synthesis loop is reliable.

## Validation Summary

- Bundle preparation status: `Ready`
- Bundle readiness gate: `Passed prepared-stage structural validation`
- Execution status: `Completed`
- Subbundle gate review: `All subbundles completed in dependency order`
- Final closure gate: `Passed completed-stage validator`
- Browser validation analytics: `Not applicable - domain/API-only changes; no UI route changed`

## Executive Findings From Current Implementation

1. **Consolidation is currently item-centric, not cluster-centric.** `CognitiveMemoryConsolidationService.ExecuteRunAsync` loads source items and promotes them one by one into candidates and records. It does not group related source items, form evidence clusters, merge duplicates, or create aggregate memories before mutation.
2. **Dreaming modes exist in contracts but do not yet have distinct behavior.** `ProjectNightly`, `CrossProjectWeekly`, `ProcedureMining`, `FailureLearning`, `KnowledgeCoverageRefresh`, `EpistemicDriveScan`, and `LearningOpportunityReview` are defined, but the current engine largely uses the same per-source pipeline with heuristic candidate-kind selection.
3. **The fact extractor is shallow and domain-biased.** `CognitiveMemoryConsolidationFactExtractor` uses deterministic line selection and a small set of business-plan/planning dimensions. This is useful for tests but not enough for general dream consolidation.
4. **Topic keys and claims are too coarse for high-quality clustering.** The applicator builds topic keys from source system/type/kind/title and creates generic claims with predicate `is-grounded-by`. That prevents robust semantic grouping and sentence-level provenance.
5. **Recall scoring is stronger than recall synthesis.** The recall orchestrator has useful channels and score traces, but the agent context package mostly forwards selected context sections and locators. It does not yet produce a concise synthesized answer/brief with hidden-but-resolvable provenance.
6. **There is a recall decision integrity issue to check immediately.** `SelectFocus` preserves only inhibited candidates, then converts other non-inhibited candidates to `Selected`; this can accidentally promote `SideContext` candidates into selected focus unless it is fixed.
7. **Previous validation bundles prove core ingestion/recall mechanics, not deep dream quality.** The multi-cycle validation demonstrated recall and project-scope behavior, but vector was unavailable in that run and there was no proof of aggregate dreaming, cluster validation, or reference-on-demand synthesis.

## Recommended Implementation Order

Run the subbundles in order. Subbundle 01 establishes diagnostics and failing tests, Subbundle 02 creates the clustering substrate, Subbundle 03 implements dreaming orchestration, Subbundle 04 adds aggregate/provenance records, Subbundle 05 hardens validation and review gates, Subbundle 06 changes how memories are consumed, and Subbundle 07 proves the whole loop end-to-end.

## Subbundles

| Order | Subbundle | Purpose |
|---:|---|---|
| 01 | `01-current-implementation-quality-audit` | Produce baseline metrics, fail-fast tests, and source-level quality findings. |
| 02 | `02-multi-key-clustering-foundation` | Add durable cluster keys, cluster members, and cluster planning across multiple key families. |
| 03 | `03-dreaming-consolidation-engine` | Implement explicit dream runs that work on clusters and produce aggregate candidates. |
| 04 | `04-aggregate-memory-claim-provenance` | Persist aggregate memories with claim-level evidence mapping and provenance drill-down. |
| 05 | `05-dream-validation-review-gates` | Add multi-step validation and review gates for aggregate memories before activation. |
| 06 | `06-retrieval-synthesis-reference-on-demand` | Convert recall results into concise synthesized briefs with optional reference expansion. |
| 07 | `07-end-to-end-quality-validation-corpus` | Build a regression corpus and prove clustering, dreaming, validation, synthesis, and references. |

## Main Source Anchors

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationFactExtractor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationCandidateApplicator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallEvaluation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallContextPackBuilder.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAgentContextPackage.cs`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\validation\evidence\20260517-181521\99-run-summary.json`
