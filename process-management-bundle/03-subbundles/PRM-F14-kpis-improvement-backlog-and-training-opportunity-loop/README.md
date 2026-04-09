# PRM-F14 — Operational intelligence, improvement backlog, and training-opportunity loop

## Objective

Turn runtime telemetry and repeated deviations into governed improvement requests, training-opportunity markers, and curator-ready signals without contaminating live execution.

## Priority and wave

- Priority: **Medium**
- Planned wave: **Wave 4**
- Depends on: **PRM-F08, PRM-F19, PRM-F21**

## Why this feature exists

The operating-model review made it clear that improvement and training signals should come from real telemetry and conformance evidence rather than abstract KPI placeholders.

## In scope

- Improvement requests derived from telemetry and conformance findings
- Training-opportunity markers separated from live execution state
- Curator-ready insight surfaces for later intelligence-lake integration
- Owner/governance review routing for improvement candidates

## Non-goals

- Do not block operational process delivery on the intelligence lake.
- Do not let training markers pollute normal live execution queries.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/ProcessInsightsService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessImprovementModels.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessInsightsIntegrationTests.cs`