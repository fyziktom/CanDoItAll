---
name: csharp-modular-refactoring
description: "Refactor large C# classes, partial-class clusters, god objects, broad managers, and responsibility-heavy runtime files into cohesive services, strategies, builders, factories, providers, adapters, and projects with tests."
---

# C# Modular Refactoring

Use this skill when a class is too large, a partial-class cluster hides unrelated responsibilities, or a feature is being added to an already overloaded runtime, manager, module, dispatcher, or service.

## Goal

Move from file-based separation to responsibility-based separation. A correct refactor creates independently testable behavior and reduces the old class to a thin orchestrator or facade.

## Non-negotiable rules

- Do not add a partial class as the final architecture boundary.
- Do not hide behavior in nested classes inside the large class.
- Do not create `Helpers`, `Utils`, `Common`, or `Manager` types that mix unrelated responsibilities.
- Do not move code before understanding dependency direction.
- Do not create cyclic references to make extraction compile.
- Do not duplicate moved logic in the old class.
- Do not close without tests that instantiate the extracted type directly.

## Required flow

1. Inventory the large class and all partial files.
2. Use `candoitall-codeanalytics-mcp` when available: inspect findings or hotspots, exact symbols, `symbol_members_get`, references, and focused context for the candidate responsibility slice.
3. Group methods, fields, nested types, and dependencies by responsibility.
4. Pick one cohesive slice. Do not refactor every responsibility in one pass unless the bundle explicitly owns that scope.
5. Add characterization tests if the behavior is currently fragile or high-risk.
6. Extract stable contracts first when callers and implementations need to move apart.
7. Move one responsibility into a top-level type.
8. Move to a dedicated project when the responsibility has separate packages, SDKs, drivers, provider implementations, or independent tests.
9. Replace old logic with a thin delegation path.
10. Wire the extracted type through dependency injection, a factory, a builder, or a catalog provider.
11. Delete old duplicate logic.
12. Add isolated unit tests for the extracted type.
13. Add composition or integration smoke if runtime wiring changed.
14. Run the architecture review gate.

## Responsibility inventory template

```markdown
| Source | Member | Responsibility | Dependencies used | Target owner | Test seam | Risk |
|---|---|---|---|---|---|---|
| Runtime.Tools.cs | BuildToolDefinitions | Tool catalog construction | tool metadata, security policy | Tool catalog provider project | instantiate provider with fake policies | high |
```

## Safe extraction sequence

Prefer this sequence:

```text
1. Extract immutable records and interfaces.
2. Create extracted implementation type in the current project.
3. Add direct unit tests.
4. Move the implementation type to a dedicated project if dependencies justify it.
5. Add project reference from composition root or runtime adapter only.
6. Replace old method with delegation.
7. Delete old implementation code.
8. Prove no new partial class was added.
```

## Shallow-pass traps

A refactor is shallow when:

- old class still contains all provider-specific branches
- extracted service only returns hardcoded sample data
- tests only prove that DI resolves
- new project contains only wrappers while logic stays in old partial file
- old and new code paths coexist without proving production uses the new path
- the new factory returns the old class for every case

For each critical refactor, add one negative test or source assertion that would fail if the shallow version were implemented.

## Proof required

- before/after responsibility map
- CodeAnalytics snapshot id, symbol/member evidence, and relevant findings or hotspots when available
- changed source file list
- direct unit tests for extracted behavior
- build and targeted test transcript
- source assertion that moved behavior no longer lives in the old class
- partial-class policy result
- dependency graph result when project references changed

## Exit condition

The refactor is complete only when the old class owns fewer responsibilities, extracted behavior has a cohesive owner, tests can exercise it without the old runtime, dependency direction is valid, and the next similar extension can be added without another partial-class expansion.
