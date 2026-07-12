---
name: csharp-architecture-governor
description: "Coordinate high-quality C# architecture design before implementation. Use when a task touches large-class refactoring, partial classes, tools, providers, memory protocols, runtime composition, process drivers, workflow executors, project references, builders, factories, or other extension-heavy .NET code."
---

# C# Architecture Governor

Use this skill before editing C# production code when the task is architecture-heavy or likely to become architecture-heavy.

The goal is to prevent fake modularity. A change is not architecturally sound merely because it adds interfaces, extension methods, nested classes, or partial files. The design must reduce responsibility concentration, create real test seams, preserve dependency direction, and make future extension easier without returning to the original monolith.

## Operating posture

- Start from repository evidence, not memory.
- Use `candoitall-codeanalytics-mcp` as the default read-only orientation tool when it is available. Build a scoped snapshot, inspect dashboard health, dependencies, findings, and exact symbols before broad manual file walking.
- Prefer small cohesive types over broad managers.
- Prefer project boundaries when a responsibility has independent lifecycle, SDK dependencies, extension growth, or tests.
- Keep contracts and abstractions free of implementation details.
- Keep composition root wiring separate from runtime behavior.
- Treat partial-class growth as a blocked design unless it passes the partial-class policy.
- Require tests that exercise extracted behavior without constructing the old large class.
- Require proof that the old class shrank or became a thin facade.

## When to route to this skill

Use this skill if any of these signals appear:

- a class or partial-class group owns multiple responsibilities
- adding a new tool/provider/driver requires editing a runtime partial class
- implementation is scattered across partial files
- a new project reference is needed
- a cyclic reference is possible
- new memory provider, MCP connector, plugin, workflow executor, process driver, or capability catalog is being added
- complex construction logic is being placed into `Program.cs`, a runtime class, or a module initializer
- tests are hard because behavior requires full runtime construction

## Required flow

1. Build or reuse the narrowest useful CodeAnalytics snapshot for the affected solution, projects, or namespaces when the MCP is available.
2. Use CodeAnalytics dashboard, solution/project inventory, dependencies, findings, and exact symbol tools to identify owners, references, hotspots, and source files.
3. Read the relevant solution file, project files, and source files that own the current behavior.
4. Build a responsibility inventory using `../_csharp-architecture-shared/references/responsibility-slicing-rubric.md`.
5. Identify whether the work is:
   - local extraction
   - project-boundary extraction
   - construction model design
   - provider/tool/plugin isolation
   - dependency graph repair
   - testability repair
6. Select the downstream architecture skills:
   - `csharp-modular-refactoring` for large classes and partial clusters
   - `csharp-project-boundary-extraction` for new or corrected project boundaries
   - `csharp-factory-builder-composition` for construction and catalog assembly
   - `csharp-provider-tool-plugin-isolation` for extension-heavy surfaces
   - `csharp-dependency-graph-audit` for project reference changes
   - `csharp-testability-contracts` for proof and tests
   - `csharp-design-pattern-selection` when pattern choice matters
   - `csharp-architecture-review-gate` before closure
7. Write an Architecture Decision Record or bundle architecture section before implementation when the change is non-trivial.
8. Define acceptance criteria that can detect shallow separation.
9. Split implementation into foundation subbundles and feature subbundles when using the CanDoItAll bundle system.
10. Add architecture checkpoints after each critical foundation subbundle.

## Architecture gate

Before implementation starts, answer these questions in durable notes or bundle files:

- Which responsibility is being isolated?
- Which type or project currently owns it?
- Which type or project should own it after the change?
- What dependency direction must be preserved?
- Which contracts must move before implementations?
- Which factory, builder, strategy, adapter, provider, or facade is justified?
- Which simpler option was rejected and why?
- Which unit tests prove the isolated behavior independently?
- Which integration smoke proves the composition root still wires the system?
- What prevents another partial file from being added next time?

## Stop conditions

Stop and redesign if:

- the plan adds another partial class to the monolith without a temporary removal path
- the plan adds a nested class instead of an independent type or project
- the new abstraction is implemented only by the old large class
- a new project reference points inward incorrectly or creates a cycle
- the design requires `IServiceProvider` service-location in core behavior
- tests cannot target the extracted responsibility without full runtime construction
- the target project would become a dumping ground for unrelated code

## References

- Read `../_csharp-architecture-shared/references/partial-class-policy.md`.
- Read `../_csharp-architecture-shared/references/project-boundary-map.md`.
- Read `../_csharp-architecture-shared/references/pattern-selection-matrix.md`.
- Read `../_csharp-architecture-shared/references/testability-gate.md`.
- Read `../_csharp-architecture-shared/references/bundle-architecture-sections.md` when preparing a bundle.
- Use `candoitall-codeanalytics-mcp` for scoped snapshots, dependency/cycle evidence, findings, exact symbol lookup, references, implementations, and focused context.

## Exit condition

Exit only when the implementation plan has a clear responsibility split, real target boundaries, dependency-direction proof, selected patterns with justification, testability proof, and a checkpoint that prevents the original large class from continuing to grow.
