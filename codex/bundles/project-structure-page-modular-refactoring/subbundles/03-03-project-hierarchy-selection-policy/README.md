# 03 Project Hierarchy Selection Policy

## Status

- `Completed`

## Objective

Move pure attach/reconnect eligibility and cycle traversal out of the page into one top-level policy while leaving dialog and application-service orchestration in the page.

## Success Criteria

- Policy has no page/UI state dependency.
- Page delegates attach and reconnect decisions.
- Self, duplicate, ancestor, descendant, current-parent, cyclic-input, and valid-candidate tests pass.

## Covered Inputs

- `N001`-`N005`; `R001`, `R005`-`R008`.

## Prerequisites

- SB02 completed and its architecture checkpoint passed.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ProjectHierarchy.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAutomaticPlacementPolicy.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProjectStructureProcessContextNodeFilterTests.cs`

Discovery instruction: add the named hierarchy selection policy and direct/architecture test files beside these existing anchors.

## UI Composition Contract

- N/A: existing dialog and options remain unchanged.

## Deliverables

- hierarchy selection policy;
- thin page switch/delegation;
- direct graph-policy tests;
- architecture assertion that old private traversal is gone.

## Dependency Impact

- SB04 relies on this policy proof for final page responsibility reduction.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation: no.

## C# Architecture Impact

- Separates graph eligibility policy from Blazor orchestration.

## Boundary Ownership

- Policy owns graph rules; page owns mode/UI/service calls.

## Dependency Direction

- Page points to policy; policy points only to Projects hierarchy summaries.

## Pattern Decision

- Concrete policy, not strategy/factory/interface.

## Testability Contract

- Direct id/link tests with no page, database, network, or host.

## Partial Class Policy

- No new partial; hierarchy partial must shrink.

## Architecture Proof Required

- direct tests, source delegation/absence assertion, line-count delta.

## Implementation Steps

1. Add direct policy tests.
2. Add policy and move graph rules.
3. Replace page predicates.
4. Delete old private graph helpers.
5. Run tests/build and closure gate.

## Scope Exceptions

- Hierarchy persistence/application-service behavior is unchanged.

## Do Not Do

- Do not move dialog state or use the page dialog enum inside the policy.

## Acceptance Checklist

- [x] direct negative and valid cases pass;
- [x] cyclic input terminates predictably;
- [x] page delegates and owns no duplicate graph traversal;
- [x] no UI or persistence behavior changed.

## Proof Required

- targeted unit tests, Workbench build, source assertion, line counts.

## Browser Validation Logging

- N/A.

## Progression Gate

- SB04 starts after policy tests and source assertions pass.

## Reopen Triggers

- Incorrect candidate list, hierarchy cycle, page-state dependency, or duplicated graph rule.

## Closure Evidence

- The page partial shrank from 295 to 234 lines.
- The 102-line policy adds one named, directly testable owner; total production impact is 41 lines for explicit self checks and a typed traversal delegate.
- Ten direct graph-policy cases and six source architecture cases pass within the 31/31 combined focused gate.
- Workbench builds with zero errors; no UI, persistence, DI, interface, partial, or project-reference change was introduced.
