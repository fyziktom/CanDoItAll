# Codex task — PRM-F19

Implement **Outcome metrics, capacity, wait-state telemetry, and customer-value measures** inside the uploaded CanDoItAll solution.

## Constraints

- Treat `CanDoItAll.Modules.Processes` as the canonical owner for process-management behavior.
- Reuse CRM-HR, Activity, Automation, Validation, TestLab, and Security seams where the bundle says so.
- Do not add direct compile-time dependency on the uploaded AgentFramework repo.
- Keep all code comments in English.
- Preserve buildability for the current solution layout.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

- The runtime distinguishes active work time, waiting time, approval wait, blocked time, and rework loops.
- Metrics include lead time, touch time, queue time, first-time-right, rework rate, bottleneck steps, capacity load, and SLA attainment.
- Dashboards can segment by process, owner, customer, project, interface, and criticality tier.
- Raw activity counters are not presented as success KPIs without outcome context.
- Customer or internal-customer feedback signals can be attached to completed runs or outputs.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/ProcessTelemetryModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessMetricsService.cs`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessMetricsPage.razor`
- `src/CanDoItAll.SharedKernel/ActivityStream.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessMetricsIntegrationTests.cs`