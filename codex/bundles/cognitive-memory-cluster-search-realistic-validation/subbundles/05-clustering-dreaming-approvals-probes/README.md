# 05-clustering-dreaming-approvals-probes

## Status

- `Completed`

## Objective

Observe ingestion, clustering, dreaming, approval decisions, probes, and recall against source-truth data, then compare memory output against the source truth.

## Covered Inputs

- REQ-08, REQ-09.

## Prerequisites

- Subbundle 04 completed with source ingestion or blocked with a partial-validation path.
- Provider/model settings are available or the blocker is recorded.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.OperationsEndpoints.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.AdvancedEndpoints.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.RecallReviewEndpoints.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryClusterPlanner.cs`

## Deliverables

- Consolidation/dreaming run evidence.
- Cluster search observations.
- Approval decisions for useful/noisy items.
- Probe sessions for missing or incorrect information.
- Recall comparison notes against source truth.

## Dependency Impact

- Trouble capture and architecture follow-up depend on this evidence.

## Validation Depth

- Behavior-critical and exploratory.

## Implementation Steps

1. Run consolidation/dreaming through supported APIs.
2. Inspect snapshot and cluster search results.
3. Approve useful review items and reject noisy items.
4. Run probes where memory misses source-truth facts.
5. Run recall queries and compare against source truth.
6. Record operation IDs, trace IDs, mismatches, and approvals.

## Do Not Do

- Do not approve PII-heavy or source-poor memories.
- Do not hide provider failures.
- Do not claim long-term validation from one short run.

## Acceptance Checklist

- Dreaming/consolidation result captured or blocked with cause.
- Approval decisions captured where available.
- Probe/recall traces captured where available.
- Source truth comparison notes recorded.

## Proof Required

- API captures under `proof/api`.
- Workbook rows updated.
- Execution report summary.

## Browser Validation Logging

- Cluster Search browser proof should show the resulting cluster state when available.

## Progression Gate

- Subbundle 06 starts after observations or blockers are recorded.

## Suggested Agent Prompt

```text
Execute subbundle 05. Run Cognitive Memory consolidation/dreaming, approvals, probes, and recall via API where available. Compare against source truth and record mismatches.
```
