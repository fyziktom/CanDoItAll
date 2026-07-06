---
name: csharp-project-boundary-extraction
description: "Design and execute C# project-boundary extraction for Abstractions, Contracts, Core, Runtime, Providers, Tools, Drivers, Persistence, Projections, and composition-root wiring while preventing cyclic references."
---

# C# Project Boundary Extraction

Use this skill when extracting a responsibility into a separate project or when adding a new project for providers, tools, memory protocols, process drivers, workflow executors, or infrastructure implementations.

## Goal

Create real compile-time boundaries that improve testability and prevent runtime classes from knowing every concrete implementation.

## Boundary decision criteria

A separate project is justified when at least one is true:

- the code has external SDK or package dependencies that core should not reference
- the code has multiple implementations or is expected to grow by extension
- the code needs independent unit tests
- the code has a different lifecycle from the caller
- the code belongs to a known layer such as Abstractions, Contracts, Core, Application, Runtime, Drivers, Providers, Tools, Persistence, or Projections
- the current location would force a cyclic reference
- the feature should be usable by multiple modules without dragging UI/runtime dependencies

## Required flow

1. Read the solution file and relevant `.csproj` files.
2. Use `candoitall-codeanalytics-mcp` when available: build a scoped snapshot, inspect solution/project inventory, run `dependencies_get`, and search exact symbols for contracts, implementations, factories, and callers.
3. Draw current project references for the affected area.
4. Classify contracts, abstractions, domain logic, runtime coordination, implementations, and composition wiring.
5. Decide whether a new project is needed or an existing project is the correct owner.
6. Move shared contracts before implementations.
7. Ensure contracts do not reference implementation projects, SDKs, UI, persistence, or module projects.
8. Add implementation project references only in the correct direction.
9. Wire implementation in the composition root or module registration extension.
10. Add tests near the boundary:
   - unit tests for core/application behavior
   - implementation tests for provider/driver/persistence behavior
   - composition smoke when registration changed
11. Run build and targeted tests.
12. Refresh CodeAnalytics dependency proof when project references changed.
13. Record dependency-direction proof.

## Recommended project naming

Use names that express role and domain:

```text
CanDoItAll.AgentFramework.Tools.Abstractions
CanDoItAll.AgentFramework.Tools.Standard
CanDoItAll.AgentFramework.Memory.Abstractions
CanDoItAll.AgentFramework.Memory.Mem0
CanDoItAll.Processes.Drivers.Abstractions
CanDoItAll.Processes.Drivers.Standard
CanDoItAll.Processes.Runtime
CanDoItAll.Processes.Builder
```

Avoid names such as:

```text
CanDoItAll.Common
CanDoItAll.Helpers
CanDoItAll.SharedStuff
CanDoItAll.RuntimeExtensions
CanDoItAll.Managers
```

## Dependency patterns

Prefer:

```text
Runtime -> Abstractions
ProviderImplementation -> Abstractions
CompositionRoot -> Runtime
CompositionRoot -> ProviderImplementation
Tests -> Abstractions
Tests -> ProviderImplementation
```

Avoid:

```text
Abstractions -> ProviderImplementation
Core -> Persistence
Core -> UI
ProviderImplementation -> Runtime monolith
Runtime -> every concrete provider implementation
```

## Cycle repair

If extraction creates a cycle:

1. Stop adding references.
2. Identify the exact type causing the cycle.
3. Move that type to a smaller Contracts or Abstractions project.
4. Make both sides depend on the smaller project.
5. Keep behavior out of the contracts project.
6. Rebuild.

Do not repair cycles by moving unrelated code into a broad shared project.

## Proof required

- before/after project reference list
- CodeAnalytics snapshot id and dependency/cycle result when available
- build transcript
- test transcript
- source assertion that contracts are SDK-free
- source assertion that implementation does not require the old runtime class for unit tests
- composition registration source reference

## Exit condition

Exit only when the target project owns a cohesive responsibility, references point in the correct direction, no cycle exists, the old runtime does not directly know every concrete implementation, and tests prove the boundary independently.
