# PRM-F07 — Runtime execution state machine and assignments

## Objective

Implement process runs, step runs, state transitions, actor claims, eligibility-aware assignments, and safe concurrency rules for manual, AI, and hybrid execution.

## Priority and wave

- Priority: **Critical**
- Planned wave: **Wave 2**
- Depends on: **PRM-F02, PRM-F03, PRM-F04, PRM-F05, PRM-F06**

## Why this feature exists

This feature is part of the first process-management bundle because the user explicitly wants process definitions, actor responsibility, handoffs, and governed execution to land **before** the intelligence lake and before deep runtime coupling to the AgentFramework overlay.

## In scope

- Runs keep published-definition immutability.
- Only valid state transitions are allowed.
- Conflicting claims and double completions are rejected deterministically.
- Assignment resolution can consider eligible pools, capacity/validation state, and fallback routes before work is claimed or rebound.
- Manual, human-approved, and AI-backed executors all fit the same state machine.

## Non-goals

- Do not introduce advanced parallel orchestration in this feature.
- Do not assume a single hard-coded assignee is always known at design time.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/ProcessRunModels.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeServices.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessLeaseService.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessActorServices.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureLeaseService.cs (reference pattern)`
- `tests/CanDoItAll.Tests.Integration/ProcessRuntimeIntegrationTests.cs (new)`
