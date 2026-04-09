# PRM-F21 — Conformance, field observation, and reality-alignment reviews

## Objective

Compare modeled processes with actual execution and field observations, capture deviations and unofficial workarounds, and turn them into governed improvement work so the system reflects reality instead of only diagrams.

## Priority and wave

- Priority: **High**
- Planned wave: **Wave 4**
- Depends on: **PRM-F08, PRM-F11, PRM-F19, PRM-F20**

## Why this feature exists

The process on paper is not the same as operational reality, so the bundle must support conformance and governed field observation.

## In scope

- Conformance observations linked to runs/versions
- Deviation clustering from journal evidence
- Paper-versus-reality review surfaces
- Privacy-safe restricted observation handling

## Non-goals

- Do not create an unmanaged gossip log about individuals.
- Do not assume the official diagram is always the truth.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/ProcessConformanceModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessConformanceService.cs`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessConformancePage.razor`
- `src/CanDoItAll.Modules.Security/SecurityModels.cs`
- `src/CanDoItAll.SharedKernel/ActivityStream.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessConformanceIntegrationTests.cs`