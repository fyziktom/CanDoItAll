---
name: csharp-design-pattern-selection
description: "Select appropriate C# design patterns for architecture and refactoring work, with a strict requirement to justify each pattern by problem forces, dependency direction, and testability."
---

# C# Design Pattern Selection

Use this skill when deciding how to structure new or refactored code with factories, builders, strategies, adapters, facades, bridges, decorators, commands, pipelines, observers, state objects, or composites.

## Goal

Choose the simplest pattern that solves the actual design force. Do not add patterns to make code look architectural.

## Required flow

1. Name the problem force.
2. Check whether a simple extracted class is enough.
3. If a pattern is needed, choose one from the matrix.
4. Write a Pattern Selection Record.
5. Define new types and project locations.
6. Define unit-test seam.
7. Define how the pattern prevents future monolith growth.
8. Reject over-engineered patterns.

## Pattern Selection Record template

```markdown
# Pattern Selection Record: <name>

## Context

What code is changing and what problem force exists?

## Forces

- extension growth:
- multiple implementations:
- construction complexity:
- external SDK isolation:
- runtime selection:
- testability:
- dependency direction:

## Selected pattern

<Factory Method | Abstract Factory | Builder | Strategy | Adapter | Facade | Bridge | Decorator | Command | Chain of Responsibility | Observer | State | Composite>

## Rejected alternatives

- simpler class:
- partial class:
- switch statement:
- service locator:
- direct construction:

## New types and projects

| Type | Project | Responsibility |
|---|---|---|

## Test plan

| Test | Behavior proven |
|---|---|

## Proof that this is not fake separation

How will proof show that behavior moved out of the old class?
```

## Selection guidance

- Use Factory Method for provider/tool/driver selection.
- Use Abstract Factory for families of related provider products.
- Use Builder for complex definitions such as process, workflow, role, artifact, tool catalog, or runtime capability construction.
- Use Strategy for interchangeable algorithms and policies.
- Use Adapter for external SDKs, protocols, or transport formats.
- Use Decorator for cross-cutting behaviors around a stable interface.
- Use Chain of Responsibility for ordered validators or authorization/dispatch pipelines.
- Use Command for tool invocations or queued executable work.
- Use Composite for nested process/workflow structures.
- Use State when behavior changes by lifecycle state and switch logic is growing.
- Use Observer for event and snapshot notification.
- Use Facade only as a thin compatibility shell.
- Use Bridge only when abstraction and implementation hierarchies both vary.

## Anti-patterns

- Pattern names in class names without the pattern forces.
- A factory that contains all business logic.
- A builder that uses service location to call providers.
- A strategy that is selected by a giant switch inside the old runtime.
- A facade that becomes the new monolith.
- A decorator that changes core behavior rather than wrapping cross-cutting concerns.

## Exit condition

Exit only when the selected pattern is justified, testable, minimal, and improves future extension without hiding the old design problem.
