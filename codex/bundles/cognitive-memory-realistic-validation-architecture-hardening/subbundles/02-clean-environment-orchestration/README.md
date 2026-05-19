# 02-clean-environment-orchestration

## Status

- `Ready`

## Objective

Make clean PostgreSQL and Qdrant validation environments repeatable, visible, and safe.

## Required Edits

- Expose active database profile origin, database name, and override source in Cognitive Memory status.
- Add Qdrant collection readiness and collection list diagnostics.
- Add idempotent clean validation profile creation guidance and proof capture.

## Closure Proof

- API proof shows the active clean profile and Qdrant readiness.
- UI proof shows operators can tell which profile is active.

## Covered Inputs

- Clean PostgreSQL validation requires explicit profile identity, database name, override reason, and Qdrant readiness.

## Prerequisites

- Cognitive Memory database profile and status APIs are available for additive diagnostics.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApiDtos.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.DatabaseEndpoints.cs`

## Deliverables

- Operator-visible database/profile/projection diagnostics that make the active validation environment unambiguous.

## Dependency Impact

- Source transfer, recall, dreaming, and long-run cycles all depend on clear profile and projection-provider state.

## Validation Depth

- Use API integration tests for status shape and, when services are available, real PostgreSQL/Qdrant readiness proof.

## Implementation Steps

- Extend status contracts, populate profile/projection fields, and verify the OpenAPI route surface remains stable.

## Do Not Do

- Do not silently switch profiles or mask missing Qdrant readiness as a successful vector validation run.

## Acceptance Checklist

- Active profile source and database name are visible.
- Projection provider readiness includes actionable missing-state details.

## Proof Required

- Status endpoint proof and focused integration tests covering profile and projection diagnostics.

## Browser Validation Logging

- Record the large-screen status view and confirm the operator can distinguish active profile and projection readiness.

## Progression Gate

- Proceed only when profile and projection readiness are observable before transfer or recall validation begins.

## Suggested Agent Prompt

- Add Cognitive Memory clean-environment diagnostics for active database profile and Qdrant readiness, preserving existing API contracts where possible.
