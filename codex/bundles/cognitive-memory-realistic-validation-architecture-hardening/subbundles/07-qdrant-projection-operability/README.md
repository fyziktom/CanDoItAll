# 07-qdrant-projection-operability

## Status

- `Ready`

## Objective

Make Qdrant projection health and vector recall behavior clear enough for operators to trust validation results.

## Required Edits

- Add default projection profile diagnostics.
- Add per-project/per-collection projection summaries.
- Add explicit recall warnings for missing projection options and provider failures.

## Closure Proof

- Projection rebuild proof shows projected, failed, and skipped counts.
- Recall proof distinguishes Qdrant hits from lexical-only recall.

## Covered Inputs

- Qdrant projection worked only after explicit projection options, and recall traces needed to distinguish configured vector search from skipped or failed provider paths.

## Prerequisites

- Projection diagnostics can be exposed through the Cognitive Memory status/API contracts without making Qdrant the source of truth.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApiDtos.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.DatabaseEndpoints.cs`

## Deliverables

- Projection readiness and rebuild summaries that expose collection/profile state and vector recall stage outcomes.

## Dependency Impact

- Long-run validation cannot trust recall quality if vector projection readiness and failures are opaque.

## Validation Depth

- Integration/API tests should prove diagnostic fields and route exposure; real Qdrant proof should run in the soak environment.

## Implementation Steps

- Add default projection diagnostics, expose per-collection summaries, and ensure recall traces report configured, skipped, and failed vector stages.

## Do Not Do

- Do not treat Qdrant as authoritative storage; PostgreSQL remains source of truth and Qdrant remains rebuildable projection.

## Acceptance Checklist

- Missing projection options produce an explicit warning.
- Provider failures are visible as provider failures, not lexical success.

## Proof Required

- Status/API proof plus real Qdrant projection proof when the service is available.

## Browser Validation Logging

- Record large-screen diagnostics/projection UI proof when projection status controls are changed.

## Progression Gate

- Proceed only when vector readiness and recall-stage behavior are visible enough for validation decisions.

## Suggested Agent Prompt

- Make Cognitive Memory Qdrant projection operable by surfacing projection readiness, rebuild counts, and vector recall-stage outcomes.
