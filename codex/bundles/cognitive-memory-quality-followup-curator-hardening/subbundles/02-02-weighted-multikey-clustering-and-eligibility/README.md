# 02 Weighted Multi-Key Clustering And Eligibility

## Status

- Status: `Completed`

## Objective

Replace single-key cluster promotion with weighted composite clustering and explicit aggregate eligibility gates.

## Covered Inputs

- F-01 clustering is single-key grouping.
- RQ-02 and RQ-03.
- User request to focus on clustering by different keys.

## Prerequisites

- SB01 clustering regression tests must exist.
- Do not start deep dreaming work until this subbundle gate is closed.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityEntities.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.Quality.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemoryClusterSearchTab.razor

## Deliverables

- Cluster signal model with key family weights and signal strength.
- Cluster cohesion, source independence, source diversity, size, contradiction, access/risk, and readiness/eligibility metrics.
- Low-signal key families used only as supporting signals unless combined with strong semantic/evidence signals.
- Persisted/displayed metrics sufficient for review and cluster search.

## Dependency Impact

- Blocks SB03 and SB05.
- A bad cluster scorer invalidates dream validation, professor assimilation, and recall synthesis proof.

## Validation Depth

- Unit tests for low-signal-only groups not becoming aggregate-ready.
- Unit tests for good composite semantic/evidence clusters becoming eligible.
- Tests for source topology granularity and relation graph behavior.
- Persistence tests for metrics and cluster search display if UI changes.

## Implementation Steps

- Introduce a cluster signal extraction/scoring design or equivalent internal services.
- Classify key families as strong, supporting, guard, or negative.
- Change default planning so broad keys cannot create aggregate-ready primary clusters alone.
- Add max cluster size/split or review behavior for overbroad candidates.
- Update tests that previously expected all key families as primary clusters.
- Surface quality metrics in quality/cluster search UI if the existing UI needs operator visibility.

## Scope Exceptions

- Do not implement economic attention or memory cost scoring.
- Do not require full NLP NER; deterministic token/entity extraction can be improved incrementally if weighted safely.

## Do Not Do

- Do not preserve `ProjectScope`, `Temporal`, or `AccessRisk` as aggregate-ready primary clusters by default.
- Do not silently drop restricted/redacted policy checks.

## Acceptance Checklist

- Default planner no longer promotes low-signal-only clusters to aggregate-ready.
- Composite clusters expose enough metrics for validators and operators.
- All updated quality tests pass.
- Execution report includes before/after cluster counts for regression seeds.

## Proof Required

- Targeted unit test output.
- Optional component/UI test output if metrics are surfaced.
- Execution report row updated with cluster gate result.

## Implementation Evidence

- Reworked clustering to primary strong key families with supporting low-signal keys, persisted cohesion/source/composite/eligibility metrics, and blocked broad low-signal aggregate readiness.
- Surfaced aggregate eligibility metrics in quality and cluster search UI; component proof covers rendered metric text and eligibility badge.

## Browser Validation Logging

- Route: `/cognitive-memory` Cluster search or Quality tab if cluster metrics are displayed.
- Capture large desktop screenshot if UI changed.
- N/A if no UI surface is modified.

## Progression Gate

- SB03 may start only after regression seeds prove low-signal clusters are ineligible or review-only.
- SB05 may start only after professor-anchor clustering hooks can consume cluster metrics.

## Suggested Agent Prompt

Rework Cognitive Memory clustering from single-key grouping into weighted multi-key cluster scoring. Treat low-signal keys as features, not automatic aggregate-ready clusters. Preserve provenance/policy and prove behavior with the regression corpus.
