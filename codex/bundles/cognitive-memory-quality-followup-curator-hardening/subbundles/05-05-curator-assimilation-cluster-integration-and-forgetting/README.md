# 05 Curator Assimilation Cluster Integration And Forgetting

## Status

- Status: `Completed`

## Objective

Turn curator/professor mode from one-shot trusted mutation into a learning loop that improves clusters/aggregates and fades professor anchors after assimilation.

## Covered Inputs

- User professor-student learning analogy.
- F-07 missing assimilation.
- RQ-09 and RQ-10.

## Prerequisites

- SB02 cluster quality metrics must exist.
- SB03 dream lineage/invalidation must exist.
- SB04 target-safe capture model must exist.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedEntities.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemoryCuratorTab.razor
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.Quality.cs

## Deliverables

- Professor anchor lifecycle with states such as active, comparing/integrating, assimilated, faded/retired, and contradicted/rejected.
- Cluster scoring integration so active professor anchors can strengthen, weaken, split, or review clusters.
- Dream validation integration so professor contradictions or corrections affect candidate approval and stale aggregate invalidation.
- Assimilation/fading rules based on stable derived memories, repeated usage, or independent support.
- Operator-visible anchor state or diagnostics if useful.

## Dependency Impact

- Blocks SB06 provenance expansion and final quality closure.
- Future economic memory governance will depend on this lifecycle but must not be implemented here.

## Validation Depth

- Unit tests for new anchor lifecycle transitions.
- Tests that professor correction triggers targeted re-clustering/revalidation.
- Tests that anchor fading is blocked until derived stable knowledge exists.
- Tests that faded anchors remain traceable but lower retrieval weight.

## Implementation Steps

- Create professor anchor entity/service or adapt existing curator capture records while preserving lifecycle semantics.
- Write assimilation comparison logic that links anchors to clusters, candidate validations, and derived memories.
- Add targeted scheduling/invalidation hooks for clusters and dream candidates impacted by professor anchors.
- Implement fade/retire rules that preserve provenance.
- Surface lifecycle diagnostics if needed for operator trust.

## Scope Exceptions

- Do not implement economic decay/attention market scoring.
- Do not delete professor source evidence when fading; only lower active reliance/retrieval weighting.

## Do Not Do

- Do not keep professor turns as permanent high-weight memories without assimilation state.
- Do not fade anchors that have unresolved contradictions or no stable derived memory.

## Acceptance Checklist

- Professor anchor improves or corrects cluster/dream outcome in tests.
- Anchor can become assimilated only after derived memory proof.
- Faded anchor remains referenceable on demand.
- No economic governance code is introduced.

## Proof Required

- Advanced service tests for lifecycle.
- Quality tests for cluster/dream integration.
- Optional UI/component proof if lifecycle is shown.

## Implementation Evidence

- Added professor anchor state to persisted curator captures and registered `ICognitiveMemoryProfessorAnchorService`.
- Assimilation requires stable derived memory proof in the same project; fading is blocked until the anchor has assimilated.
- UI capture summaries can show target and anchor state badges when capture data exists.

## Browser Validation Logging

- Route: `/cognitive-memory` Curator or Quality tab if anchor states are surfaced.
- N/A if lifecycle remains backend-only in this phase.

## Progression Gate

- SB06 may start only after professor-anchor provenance can be followed from synthesized recall references.

## Suggested Agent Prompt

Implement professor anchor assimilation. Curator facts should become high-trust anchors that compare against clusters/dreams, drive targeted revalidation, and fade only after stable derived knowledge internalizes the lesson.
