# Implementation spec — PRM-F19

## Core implementation moves

- Extend runtime evidence capture to distinguish work, wait, blocked, and rework periods.
- Compute outcome metrics and bottleneck signals from journals plus capacity context.
- Expose dashboards that segment by owner, customer, interface, project, and criticality.

## Detailed expectations

1. Keep comments in source code in English.
2. Preserve SQLite compatibility and keep PostgreSQL migration parity where storage is touched.
3. Respect Workbench projection-only guardrails whenever Workbench surfaces are involved.
4. Reuse existing CanDoItAll seams before introducing new shared abstractions.

## Data and service notes

- Feature id: `PRM-F19`
- Canonical owner: `CanDoItAll.Modules.Processes` with CRM-HR or Security bridges where needed.
- Cross-module touchpoints: CanDoItAll.Modules.Processes, CanDoItAll.SharedKernel

## Acceptance criteria

- The runtime distinguishes active work time, waiting time, approval wait, blocked time, and rework loops.
- Metrics include lead time, touch time, queue time, first-time-right, rework rate, bottleneck steps, capacity load, and SLA attainment.
- Dashboards can segment by process, owner, customer, project, interface, and criticality tier.
- Raw activity counters are not presented as success KPIs without outcome context.
- Customer or internal-customer feedback signals can be attached to completed runs or outputs.

## Suggested implementation order inside this feature

1. Add domain models and persistence mapping first.
2. Add services and validation rules second.
3. Add UI/editor/runtime integration third.
4. Add tests and end-to-end proof last.