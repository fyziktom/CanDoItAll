# 04 True Composite Multi-Key Clustering

## Status

- `Ready`

## Objective

- Replace single-primary-key grouping with bounded composite clustering that uses multiple positive and negative signals before a cluster exists.

## Success Criteria

- Cluster membership is no longer determined only by one key family/key value group.
- Pair/edge or equivalent composite scoring uses multiple signal families before forming clusters.
- Generic/broad keys cannot make aggregate-ready clusters by themselves.
- Related memories with different titles/topic keys can cluster when composite evidence is strong.
- Contradictions, wrong-scope relations, restricted/access mismatches, and stale records create split/review behavior.

## Covered Inputs

- Current planner groups at lines 82-86 before scoring.
- Current keys are heuristic and single-record oriented.
- Dreaming and professor assimilation depend on clusters being meaningfully coherent.

## Prerequisites

- SB03 regression corpus completed.
- Clustering failing-first tests present and mapped.

## Exact Source References

- /mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- /mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs
- /mnt/data/review_current/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- /mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs

## Deliverables

- Composite candidate-pair or graph-based cluster builder.
- Per-cluster signal explanations and edge metrics suitable for validation/debugging.
- Stable cluster identity design that separates topic/centroid identity from exact membership version when possible.
- Updated tests for merge, split, and negative guard cases.
- Updated diagnostics so cluster quality reports explain why a cluster formed.

## Dependency Impact

- Blocks dreaming and professor assimilation because both rely on high-quality cluster structure.
- Weak clustering invalidates aggregate validation and recall synthesis proof.

## Validation Depth

- Critical foundation implementation with adversarial tests and dependent smoke into dream selection.
- Performance must be bounded; add limits and metrics for candidate-pair counts.

## Implementation Steps

1. Design a bounded candidate-pair preselection strategy using project scope and key-index overlap.
2. Create a composite similarity/separation score using semantic topic, entity, task intent, evidence overlap, relations, temporal support, access/risk, and curator/professor anchor hints where available.
3. Build clusters from connected components or equivalent community detection above threshold.
4. Attach edge/signal explanations to cluster metrics or diagnostics.
5. Prevent generic-only tokens from reaching aggregate-ready state.
6. Update persistence if cluster edge/explanation records are needed.
7. Run clustering regression tests and one dependent dream-selection smoke.

## Scope Exceptions

- Embedding-based semantic similarity may be deferred if deterministic claim/token normalization is implemented and tested first.
- Exact clustering algorithm can be graph components, union-find, or another bounded method if it satisfies behavior tests.

## Do Not Do

- Do not keep `GroupBy(family,key)` as the primary cluster-formation mechanism.
- Do not solve by tuning thresholds on the old single-key grouping path.
- Do not make every same-project record a candidate without bounded preselection.

## Acceptance Checklist

- Old single-key grouping path is removed or demoted to candidate preselection only.
- Related different-title/topic test passes.
- Unrelated same-source/month/project test passes.
- Contradictory same-topic test routes to split/review.
- Cluster diagnostics show multi-signal reasons.

## Proof Required

- Targeted clustering unit tests.
- Performance/metrics output for candidate pair count on a representative corpus.
- Dream-selection smoke confirming aggregate-ready clusters are meaningful.
- Execution report semantic proof gate for clustering.

## Browser Validation Logging

- N/A; backend clustering behavior.
- No browser screenshots required unless cluster UI diagnostics are changed.

## Progression Gate

- SB05 and SB06 may use clustering only after merge/split/adversarial tests pass and the execution report explains the new formation algorithm.
- If clustering is still single-key grouping, this gate fails.

## Suggested Agent Prompt

```text
Implement true composite multi-key clustering. Do not tune the old single-key grouping path. Prove merge, split, contradiction, and generic-key guard behavior.
```
