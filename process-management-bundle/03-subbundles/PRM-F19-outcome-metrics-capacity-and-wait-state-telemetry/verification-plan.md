# Verification plan — PRM-F19

## Expected verification outcomes

- The runtime distinguishes active work time, waiting time, approval wait, blocked time, and rework loops.
- Metrics include lead time, touch time, queue time, first-time-right, rework rate, bottleneck steps, capacity load, and SLA attainment.
- Dashboards can segment by process, owner, customer, project, interface, and criticality tier.
- Raw activity counters are not presented as success KPIs without outcome context.
- Customer or internal-customer feedback signals can be attached to completed runs or outputs.

## Automated tests

- Unit tests for new invariants and validation rules
- Integration tests for persistence and cross-module seams
- Component tests for editor or viewer surfaces where applicable
- Playwright coverage for the main happy path if new end-user flow is introduced

## Manual verification checklist

1. Run a process with waiting and rework and verify touch/wait/rework metrics differ correctly.
2. Inspect dashboard segmentation by owner or customer.
3. Verify raw step counts are not the only KPI shown.

## Regression concerns to watch

- Activity counts substituted for outcome metrics
- Queue time inferred incorrectly from sparse events