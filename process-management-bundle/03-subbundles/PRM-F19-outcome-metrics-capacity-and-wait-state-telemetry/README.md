# PRM-F19 — Outcome metrics, capacity, wait-state telemetry, and customer-value measures

## Objective

Track lead time, touch time, queue time, wait reasons, first-time-right, rework, capacity, bottlenecks, SLA attainment, and customer-facing value measures instead of only activity counts.

## Priority and wave

- Priority: **High**
- Planned wave: **Wave 4**
- Depends on: **PRM-F07, PRM-F08, PRM-F11, PRM-F17, PRM-F18**

## Why this feature exists

Teams often optimize visible activity while ignoring waiting and rework; the bundle now needs explicit outcome-flow telemetry.

## In scope

- Lead/touch/queue/blocked time telemetry
- Rework, first-time-right, and SLA metrics
- Capacity and bottleneck signals
- Customer-value and internal-customer outcome measures

## Non-goals

- Do not present raw activity counts as the primary KPI.
- Do not block runtime delivery on a perfect BI platform.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/ProcessTelemetryModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessMetricsService.cs`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessMetricsPage.razor`
- `src/CanDoItAll.SharedKernel/ActivityStream.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessMetricsIntegrationTests.cs`