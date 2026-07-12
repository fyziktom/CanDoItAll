---
name: csharp-dependency-graph-audit
description: "Audit C# project references, dependency direction, cyclic-reference risk, composition-root boundaries, SDK leakage, and reference changes before and after architecture refactoring."
---

# C# Dependency Graph Audit

Use this skill before and after adding project references, moving types across projects, or extracting contracts and implementations.

## Goal

Keep compile-time dependencies aligned with architecture. Project references are architecture, not plumbing.

## Required flow

1. Identify all affected projects.
2. Use `candoitall-codeanalytics-mcp` when available: build a scoped snapshot, inspect `solution_inventory_get`, and run `dependencies_get` for project, module, namespace, type dependencies, and cycles.
3. Read their `.csproj` files.
4. Record current `ProjectReference` entries.
5. Identify role of each project:
   - Contracts
   - Abstractions
   - Core
   - Application
   - Runtime
   - Builder
   - Drivers
   - Providers
   - Tools
   - Persistence
   - Projections
   - UI or Modules
   - Tests
6. Decide the intended reference direction.
7. Block references from inner projects to outer projects.
8. Extract smaller contract types when a reference would create a cycle.
9. Build after reference changes.
10. Refresh the scoped CodeAnalytics snapshot after project-reference changes when available.
11. Record before/after proof.

## Reference rules

Contracts and Abstractions may be referenced by many projects, but should not reference implementation projects.

Core should not reference UI, modules, persistence, providers, or external SDK-specific projects.

Runtime may reference abstractions and core. Runtime should not need to reference every provider implementation directly.

Provider, driver, persistence, and plugin implementations may reference abstractions and provider SDKs.

Composition root may reference implementations so it can wire them.

Tests may reference the implementation under test and the contracts needed to fake collaborators.

## Cycle handling

When a cycle appears, do not solve it by:

- moving everything into `Common`
- weakening contracts with `object`
- adding reflection-based service location
- duplicating DTOs
- making core reference infrastructure

Solve it by:

- extracting a smaller contract
- moving shared records to a Contracts project
- introducing an adapter
- moving composition wiring outward
- splitting implementation from runtime orchestration

## Proof checklist

- before project reference table
- after project reference table
- CodeAnalytics snapshot id and `dependencies_get` cycle result when available
- forbidden references checked
- build result
- test result when behavior changed
- explanation for every new reference
- cycle risk result

## Exit condition

Exit only when references match the intended architecture and the build proves the dependency graph compiles without cycles.
