# SB17 Expansion Architecture Cleanup Gate

## Status

- `Ready`

## Objective

- Review/refactor the complete SB12-SB16 expansion wave into a consistent maintainable architecture before final regression.

## Covered Inputs

- N010-N017; R017-R040.

## Prerequisites

- SB12-SB16 Completed with trusted proof; all earlier foundations remain trusted.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Projects`
- `repo://src/Modules/CanDoItAll.Modules.Workbench`
- `repo://src/Modules/CanDoItAll.Modules.Processes`
- `repo://src/Modules/CanDoItAll.Modules.Resources`
- `repo://src/App/CanDoItAll.Composition`
- `repo://tests`
- `bundle://plan/architecture-checkpoints.md`
- `bundle://reviews/01-execution-report.md`

## Deliverables

- Strict cross-story architecture gate and repairs for ownership, large-class/partial growth, duplicate scope/action/content/save/cache logic, package selection, dependency/cycles, testability, component usage, desktop UX consistency.
- Before/after responsibility/line/member/reference table for Projects, Workbench page partials, Processes dashboard, Resources page, Composition.
- Cross-story browser smoke and affected build/tests/format/dependency proof.
- Cross-story scale/performance counters and scoped anti-pattern scan, including direct Project Structure image/PDF interaction with zero browser calls.
- Explicit SB18 unlock or owner reopen list.

## Dependency Impact

- SB18 blocked until unqualified Pass.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical expansion cleanup/progression gate.

## Implementation Steps

1. Apply Checkpoints D/E to actual diffs/proof.
2. Inspect each accepted screenshot and cross-story interaction consistency.
3. Repair concrete ownership/duplication/partial/dependency/test/component/UX defects only.
4. Rerun affected, accepted performance envelopes, and cross-story smoke.
5. Record C# architecture Pass/Fail and final unlock.

## C# Architecture Impact

- Cleanup/refactor only; no new feature.

## Boundary Ownership

- Confirms each module owns semantics, integration owns cross-cutting adapters/effects/cache, and UI stays thin.

## Dependency Direction

- Refresh full affected graph; no new project/module cycle or forbidden edge.

## Pattern Decision

- Validate all PSRs and remove cargo-cult factories/facades/interfaces/commands.

## Testability Contract

- All scope/coordinator/policy/save behavior directly testable without large pages; cross-story host/browser proves production wiring.

## Partial Class Policy

- No new partial. ProjectStructure old partial responsibility must not grow for migrated concerns.

## Architecture Proof Required

- Checkpoint E/C# gate, owner shrink table, refs/cycles, package graph, duplicate/no-bypass/no-partial audit, cross-story tests/browser review.

## Scope Exceptions

- Does not add deferred formats/distributed/mobile/new user stories.

## Do Not Do

- Do not turn cleanup into a broad unrelated refactor or waive required defects for final closure.

## Acceptance Checklist

- [ ] Unqualified C# architecture Pass.
- [ ] Owners/dependencies/packages/patterns/test seams match target.
- [ ] No duplicate/bypass/new partial/service locator.
- [ ] Cross-story desktop/browser/console and affected checks pass.
- [ ] Large-source bounds and known-file/browser intent separation remain intact.
- [ ] SB18 unlock explicit.

## Proof Required

- Behavioral review record, commands/results, snapshots/dependencies, owner tables, source assertions, cross-story browser artifacts/review.

## Browser Validation Logging

- Reuse accepted large desktop routes/viewports for one representative flow per story and one cross-story open/reopen/save flow; inspect current screenshots and console/network.

## Progression Gate

- Only unqualified Pass unlocks SB18.

## Reopen Triggers

- Final regression contradiction reopens earliest owning story/foundation plus SB17; do not patch in SB18 unless purely proof/status repair.

## Suggested Agent Prompt

```text
Review and clean the entire expansion wave without adding features. Inspect ownership, partial/large classes, dependencies, packages, duplicate effects, tests, components, and desktop browser truth. Repair concrete blockers and issue an unqualified Pass or reopen exact owners.
```
