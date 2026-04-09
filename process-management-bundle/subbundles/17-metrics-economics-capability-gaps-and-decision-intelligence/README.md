# 17 Metrics, Economics, Capability Gaps, And Decision Intelligence

## Status

- `Completed`

## Objective

- Implement the analytics layer for process outcomes, cost, capability gaps, and orchestration-quality evaluation so the platform learns how well it orchestrates, not only whether a step completed.

## Covered Inputs

- `REQ-021`
- `REQ-022`
- Legacy feature `PRM-F19`
- Additional architecture notes on decision intelligence, capability gaps, execution economics, and executor relationship analysis

## Prerequisites

- `16-post-implementation-bundle-phase03-generation`

## Exact Source References

- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F19-outcome-metrics-capacity-and-wait-state-telemetry\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\02-architecture\11-metrics-capacity-and-conformance.md`
- `C:\repositories\CanDoItAll\process-management-bundle\02-architecture\IMPORTANT ADDITIONAL NOTES.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Activity`
- `C:\repositories\CanDoItAll\src\CanDoItAll.SharedKernel`

## Deliverables

- Outcome metrics:
  lead time, touch time, wait time, blocked time, rework, first-time-right, SLA attainment.
- Cost attribution seams for executor, review, validation, rework, and escalation activity.
- Capability-gap and bottleneck analytics based on role requirements and assignment outcomes.
- Decision-intelligence records that evaluate whether orchestration choices were appropriate.

## Dependency Impact

- Final conformance, learning, and management improvement work depends on these analytics being business-readable and trustworthy.
- If this subbundle is weak, the platform will measure activity counts instead of orchestration quality and business value.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Implement outcome and wait-state metrics.
2. Add cost attribution and cost-quality tradeoff hooks.
3. Add capability-gap and bottleneck analytics using runtime and staffing evidence.
4. Add decision-intelligence structures that link orchestration decisions to later outcomes.

## Scope Exceptions

- A full optimization engine is deferred, but the analytics model must already support later evaluation of orchestration quality.

## Do Not Do

- Do not present raw activity counts as the main KPI.
- Do not hardcode cost or capability logic in UI-only projections.
- Do not ignore decision quality while measuring only completion speed.

## Acceptance Checklist

- Metrics distinguish active work from waiting, blocking, approvals, and rework.
- Cost attribution has a planned home.
- Capability-gap signals are based on role and assignment evidence.
- Decision-intelligence records can evaluate orchestration quality later.

## Proof Required

- Integration tests for metric aggregation and segmentation.
- Review evidence that cost and capability signals are grounded in typed runtime data.
- Browser proof for any analytics surface added in this subbundle.

## Browser Validation Logging

- Route:
  analytics or metrics route if browser-visible UI lands here
- Viewport:
  `1920x1080`
- Evidence:
  Playwright plus screenshots required only if the analytics UI is included in the same slice

## Progression Gate

- The final conformance and learning phase may continue only when outcome, cost, capability-gap, and decision-intelligence signals are grounded in trusted runtime evidence.

## Suggested Agent Prompt

```text
Implement only the process analytics layer. Measure outcome, wait, cost, capability-gap, and orchestration-quality signals from typed process evidence rather than raw activity counts.
```
