# SB04 Instance Builder

## Status

Planned.

## Objective

Create the process definition and instance builders that compose complete process plans, including recursive subprocess plans and selected drivers/strategies.

## Covered Inputs

- REQ-004
- REQ-010
- REQ-011
- REQ-012
- REQ-013
- REQ-014

## Prerequisites

- SB02 complete.
- SB03 complete.

## Exact Source References

- `bundle://architecture/01-target-solution.md`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`
- `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateProjectionService.cs`

## Deliverables

- `CanDoItAll.Processes.Builder`
- Process composition request model.
- Definition builder.
- Instance builder.
- Driver stack selector.
- Strategy assignment factory.
- Recursive subprocess composer.
- Instance plan validator.

## Dependency Impact

- Runtime cannot start until a complete instance plan exists.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Define composition request and result models.
2. Load definitions from template or direct definition.
3. Resolve component overrides and migrations.
4. Select driver stack from run context and driver catalog.
5. Assign execution/recovery/manager/branch strategies.
6. Build artifact slots and references.
7. Recursively compose subprocess plans.
8. Validate cycles, depth, route targets, strategy existence, and artifact dependency closure.

## Scope Exceptions

No actual execution. Builder persists or returns plans only.

## Do Not Do

- Do not let dispatcher choose missing strategies later.
- Do not start subprocesses directly from runtime service paths.
- Do not allow unresolved artifact inputs to become warnings.

## Acceptance Checklist

- Builder composes normal, workflow, agent, handoff, switch, and subprocess steps.
- Subprocess recursion respects depth and cycle limits.
- Strategy IDs are present for every executable step.
- Artifact dependency graph is closed or fails predictably.

## Proof Required

- Unit tests for composition.
- Negative tests for missing strategies, cycles, depth, and missing artifact slots.
- Semantic Adequacy Gate.
- `proof/SB04/manifest.md`.
- Production Behavior Artifact Matrix for instance plan, strategy assignment, subprocess plan, and artifact slot records.

## Browser Validation Logging

- N/A.

## Progression Gate

- SB05 cannot execute until instance plans are complete and persisted.

## Suggested Agent Prompt

Implement the builder as a strict compiler from definition/template/run context into an immutable instance plan.
