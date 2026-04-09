# PRM-F11 — Activity, Automation, Validation, and TestLab hooks

## Objective

Connect process runs to timeline visibility, overdue-signal generation, validation gates, and testing evidence so process execution becomes a first-class operational surface.

## Priority and wave

- Priority: **High**
- Planned wave: **Wave 3**
- Depends on: **PRM-F04, PRM-F06, PRM-F07, PRM-F08**

## Why this feature exists

This feature is part of the first process-management bundle because the user explicitly wants process definitions, actor responsibility, handoffs, and interactive modeling to land **before** the intelligence lake and before deep runtime coupling to the AgentFramework overlay.

## In scope

- Runs can emit activity entries and automation signals without tight module coupling.
- Validation and TestLab references can be attached to steps and gates.
- Overdue steps and blocked approvals become visible in automation/operations surfaces.
- The hook design does not require the intelligence lake to exist first.

## Non-goals

- Do not couple Processes directly to Activity/Automation/Validation/TestLab internals beyond existing contracts or narrow bridges.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Activity/*`
- `src/CanDoItAll.SharedKernel/AutomationSignals.cs`
- `src/CanDoItAll.Modules.Automation/*`
- `src/CanDoItAll.Modules.Validation/*`
- `src/CanDoItAll.Modules.TestLab/*`
- `tests/CanDoItAll.Tests.Integration/ProcessCrossModuleHooksIntegrationTests.cs (new)`
