# 03 Deep Dreaming Validation And Aggregate Apply

## Status

- Status: `Completed`

## Objective

Make dream consolidation produce validated aggregate knowledge rather than copied source-memory bullet lists, and apply aggregates with calibrated confidence and lineage.

## Covered Inputs

- F-02 shallow dreaming.
- F-03 weak validation.
- F-04 overconfident aggregate apply.
- RQ-04, RQ-05, RQ-06.

## Prerequisites

- SB02 cluster eligibility gate must be closed.
- Use cluster metrics from SB02 in dream selection and validation.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityEntities.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.Quality.cs

## Deliverables

- Mode-specific dream cluster selection based on eligibility and quality metrics.
- Structured aggregate candidate payload with synthesized claims, support maps, conflict maps, uncertainty notes, and truncation warnings.
- Validation rules for overbroad/mixed clusters, weak independence, duplicate aggregates, stale curator-corrected inputs, generated loops, and unsupported claims.
- Aggregate application with calibrated confidence, dedupe, lineage, and revalidation/invalidation hooks.

## Dependency Impact

- Blocks SB05 professor assimilation and SB06 reference expansion.
- Bad aggregate lineage prevents reference-on-demand from being trustworthy.

## Validation Depth

- Unit tests for good, broad, mixed, contradictory, duplicate, stale, restricted, redacted, and all-generated candidates.
- Aggregate application tests for calibrated confidence and dedupe.
- Tests proving copied source list is not accepted as deep synthesis unless explicitly carry-through.

## Implementation Steps

- Refactor dream candidate builder so aggregate text and claims are synthesized from cluster evidence.
- Add source independence and support-strength metrics to source maps or validation inputs.
- Add validator rule structure if needed to keep rules testable.
- Calibrate aggregate memory/claim confidence from validation strength rather than setting 1.0 unconditionally.
- Add aggregate lineage relations/source maps sufficient for later reference expansion.
- Ensure curator-superseded source memories invalidate or block pending aggregates.

## Scope Exceptions

- Do not require live LLM semantic entailment for CI; deterministic support heuristics are acceptable as the first version.
- Do not auto-apply candidates that need review.

## Do Not Do

- Do not create aggregate candidates from overbroad clusters just because every copied claim has a source map.
- Do not mark all aggregate claims as `DisplayBeliefScore = 1` without quality justification.

## Acceptance Checklist

- Dream run over regression corpus produces meaningful structured aggregate claims.
- Broad/mixed/weak candidates are rejected or routed to review.
- Applied aggregates preserve claim-level and aggregate-level lineage.
- All targeted quality tests pass.

## Proof Required

- Targeted unit test output.
- Database assertion proof for aggregate lineage records.
- Execution report row updated.

## Implementation Evidence

- Dream candidates now synthesize source-backed aggregate text instead of copying bullets from source memories.
- Validator rejects weak or noneligible clusters, duplicate aggregates, weak source independence, and unsupported synthesized claims.
- Aggregate apply is idempotent by stable hash and calibrates confidence below 1.0.

## Browser Validation Logging

- Route: `/cognitive-memory` Quality tab if dream metrics/warnings are displayed.
- N/A if backend-only.

## Progression Gate

- SB05 may start only after professor anchors can invalidate/revalidate dream candidates.
- SB06 may start only after aggregate provenance is rich enough for expansion.

## Suggested Agent Prompt

Deepen dream consolidation and validation. Replace copied-list aggregate text with structured synthesized claims, support/conflict/uncertainty maps, strong validation gates, calibrated aggregate apply, dedupe, and lineage.
