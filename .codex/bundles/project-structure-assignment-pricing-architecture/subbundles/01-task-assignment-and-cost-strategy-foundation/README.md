# Task Assignment and Cost Strategy Foundation

## Status

- `Completed`

## Objective

- Establish independently testable assignment-resolution and resource-cost strategy boundaries before changing persistence behavior.

## Success Criteria

- Valid multiple person/agent assignments resolve without conflict and retain the complete canonical set.
- The cost service dispatches to exactly one strategy per strongly typed resource kind.
- Person, Agent, Workflow, and Process algorithms live in cohesive top-level types.
- No new partial class or project reference is introduced.

## Covered Inputs

- `N001` architecture prerequisite, `N003`, `N005`, and requirements `R001`, `R002`, `R003`, `R007`.

## Prerequisites

- Prepared bundle validator passes.
- Existing Gantt projection characterization proving mixed assignments are valid remains green.
- Architecture records `00` through `04` pass entry review.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureTaskResourceCostService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureTaskDetailsService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureGanttTaskEditCoordinator.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ComponentAdapters.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/Hr/HrAgentUsageAnalyticsService.cs`

## UI Composition Contract

- N/A for layout: the foundation changes no markup, CSS, dialog size, first viewport, or scroll owner.
- The dependent UI remains the existing wide Gantt task dialog.

## C# Architecture Impact

- Critical extraction from a switch-heavy service and duplicated page/coordinator policy.

## Boundary Ownership

- Workbench owns resolver/strategy contracts; AgentFramework owns its Agent estimator implementation.

## Dependency Direction

- Preserve existing references; Workbench must not reference the AgentFramework module.

## Pattern Decision

- Strategy for variable price algorithms; simple pure class for assignment resolution.

## Testability Contract

- Instantiate every extracted owner without `ProjectStructurePage` or a full host.

## Partial Class Policy

- No new partial; remove duplicated selection policy from the existing partial.

## Architecture Proof Required

- Direct positive/negative tests, source assertions, `.csproj` diff, build, and architecture gate.

## Deliverables

- Immutable assignment resolution and resolver.
- Four resource-cost strategies and a thin dispatcher.
- Exact DI registrations and isolated tests.

## Dependency Impact

- `SB02` depends on complete-set resolution and exact source selection; weak proof invalidates all downstream mutation behavior.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation: yes; task assignments, cost calculation, DI, and downstream mutations.

## Implementation Steps

1. Add failing characterization and registration-negative tests.
2. Extract assignment resolution and replace duplicate mappings.
3. Extract strategies and reduce the cost service to validation/dispatch.
4. Register implementations in the existing dependency direction.
5. Run isolated tests/build and architecture checkpoint.

## Scope Exceptions

- Persistence-time repricing belongs to `SB02`; scalar-to-multiselect UI redesign is out of scope.

## Do Not Do

- Do not retain provider branches in the dispatcher, add a fallback strategy, add project references, nested strategies, broad helpers, or another partial.

## Acceptance Checklist

- [x] mixed assignment resolution passes.
- [x] all four direct strategy groups pass.
- [x] missing/duplicate registrations fail explicitly.
- [x] Agent selection never calls CRM workforce pricing.
- [x] duplicate page/coordinator policy is removed.
- [x] affected build passed; independent implementation architecture gate passed with non-blocking follow-up recorded by SB03.

## Proof Required

- Exact resolver/strategy test filters, affected builds, source assertions, and updated architecture review.

## Browser Validation Logging

- N/A at this foundation; `SB02`/`SB03` own rendered proof.

## Progression Gate

- `SB02` unlocked after direct tests, build, no-new-reference/partial assertions, and the entry architecture gate. The terminal independent implementation review subsequently passed with non-blocking follow-up.

## Reopen Triggers

- Reopen if adding a resource requires dispatcher edits, mixed assignments are lost, DI resolves zero/multiple strategies, or a reference/cycle appears.

## Suggested Agent Prompt

```text
Extract and prove the assignment resolver and cost strategies only. Preserve quote behavior and the complete assignment set, use existing dependency direction, add direct positive/negative tests, and stop if the architecture gate cannot pass.
```
