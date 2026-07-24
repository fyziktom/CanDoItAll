# 02 Shared Process Launch-Context Boundary

## Status

- `Completed`

## Objective

Move duplicated process summary/output-root policy from the page and agent service into one top-level immutable builder used by both production paths.

## Success Criteria

- One production implementation owns traversal, filtering, limits, visual-target rules, redaction, output-root precedence, and aliases.
- Both old owners delegate and contain no duplicate implementation.
- Direct positive, negative, boundary, and source architecture tests pass.

## Covered Inputs

- `N001`-`N005`; `R001`-`R004`, `R007`, `R008`.

## Prerequisites

- SB01 completed and prepared bundle validator passed.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Processes.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessNodeService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessContextNodeFilter.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProjectStructureProcessContextNodeFilterTests.cs`

Discovery instruction: add the named launch-context builder and direct/architecture test files beside these existing anchors.

## UI Composition Contract

- N/A: no markup, CSS, dialog, viewport, or interaction change.

## Deliverables

- immutable launch-context result and builder;
- thin direct calls from page/service;
- deleted duplicate methods/usings;
- direct unit and source architecture tests.

## Dependency Impact

- Critical architecture foundation. SB03/SB04 depend on proof that local extraction creates a real owner.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation: yes.

## C# Architecture Impact

- Removes a duplicated process-launch responsibility from the page partial and adjacent service.

## Boundary Ownership

- Builder owns pure projection; page/service retain orchestration.

## Dependency Direction

- Both callers depend inward on the builder; builder depends only on Workbench records/conventions.

## Pattern Decision

- Concrete immutable builder; no interface or DI.

## Testability Contract

- Tests build surfaces/nodes directly and never construct `ProjectStructurePage` or a host.

## Partial Class Policy

- No new partial. `ProjectStructurePage.Processes.cs` must shrink.

## Architecture Proof Required

- both-caller source assertion, no-old-method assertion, direct tests, line-count delta, Workbench build.

## Implementation Steps

1. Add tests for summary, output-root, aliases, limits, and negative cases.
2. Add the top-level owner.
3. Replace both production paths.
4. Delete duplicate methods and unused imports.
5. Run tests/build/source assertion and SB02 closure gate.

## Scope Exceptions

- Process dialog orchestration, staffing, estimates, execution, and persistence remain unchanged.

## Do Not Do

- Do not change output text/keys, filtering semantics, limits, or add fallback behavior.

## Acceptance Checklist

- [x] one owner exists;
- [x] both callers delegate;
- [x] duplicate methods are absent;
- [x] direct tests cover realistic positive and adversarial negative cases;
- [x] Workbench builds and downstream proof may proceed.

## Proof Required

- targeted test/build commands, source assertions, before/after line counts, and anti-stub audit.

## Browser Validation Logging

- N/A.

## Progression Gate

- SB03 starts only after all SB02 Behavioral evidence passes.

## Reopen Triggers

- Any page/agent context difference, output-root regression, bypass, duplicate implementation, or architecture-test failure.

## Closure Evidence

- `ProjectStructureProcessLaunchContextBuilder` and its immutable result own the extracted policy.
- The page and service caller files dropped from 3,622 to 2,854 lines; production code is net 341 lines smaller after adding the 427-line owner.
- Targeted direct/source tests passed 18/18.
- Isolated Workbench build passed with zero errors.
- Existing `System.Security.Cryptography.Xml` NU1903 warnings are unchanged and outside this subbundle.
