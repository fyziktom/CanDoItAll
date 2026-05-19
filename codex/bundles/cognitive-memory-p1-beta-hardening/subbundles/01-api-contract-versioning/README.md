# API Contract Versioning

## Status

- `Completed`

## Objective

- Make the Cognitive Memory HTTP contract explicit for v1 while preserving the legacy `/api/cognitive-memory` callers.

## Covered Inputs

- CM-P1-001
- CM-P1-007

## Prerequisites

- Prepared-stage bundle validator passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApiDtos.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.DatabaseEndpoints.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.RecallReviewEndpoints.cs
- C:\repositories\CanDoItAll\docs\cognitive-memory\roadmap\roadmap.md

## Deliverables

- Stable v1 contract metadata/examples or additive versioned routes.
- Legacy route compatibility.
- Docs describing common API flows and DTO expectations.

## Dependency Impact

- Later P1 API additions must appear in the v1 contract docs and avoid breaking legacy endpoints.

## Validation Depth

- API contract stabilization gate.

## Implementation Steps

1. Inspect current endpoint names and collision risk.
2. Add the smallest safe versioning surface.
3. Add examples for status, ingestion, recall, projection rebuild, automation, and review decision flows.
4. Build and update docs.

## Do Not Do

- Do not remove or rename existing route names.
- Do not duplicate route groups if endpoint names collide.

## Acceptance Checklist

- Legacy `/api/cognitive-memory` remains valid.
- v1 contract is discoverable.
- Example payloads are source-aligned.
- Documentation states the compatibility policy.

## Proof Required

- Web build.
- Targeted API/source tests if route behavior changes.
- Docs review.

## Proof Captured

- Legacy `/api/cognitive-memory` and additive `/api/cognitive-memory/v1` groups are both mapped.
- `GET /api/cognitive-memory/v1/contract` returned version `v1`, 35 routes, 7 examples, and `/api/cognitive-memory/v1/retention/cleanup`.
- Web build passed with 0 warnings and 0 errors.

## Browser Validation Logging

- Browser proof is not required unless this subbundle changes rendered UI.

## Progression Gate

- Continue only after the v1 contract surface is explicit and legacy compatibility is preserved.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Add a source-compatible Cognitive Memory v1 contract surface and examples, preserve legacy routes, run targeted validation, update the bundle execution report, and stop if endpoint naming conflicts require a design adjustment.
```
