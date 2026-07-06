# CanDoItAll C# Architecture Skills Package

This package contains a Codex skill suite for high-quality C# architecture design, modular refactoring, project-boundary extraction, provider/tool isolation, and bundle-time architecture gates.

The package is designed for repositories that have grown into large runtime classes, broad partial-class files, scattered tool/provider definitions, cyclic reference pressure, and hard-to-test orchestration logic. It is intentionally strict: a partial class is not treated as a module boundary, a nested class is not treated as a separate component, and a large facade is not treated as an architecture improvement.

## What this package adds

- A coordinator skill for C# architecture decisions before implementation starts.
- Refactoring skills for large classes, partial-class clusters, and responsibility concentration.
- Project-boundary extraction skills for Abstractions/Core/Application/Infrastructure/Runtime/Drivers style splits.
- Factory and builder guidance for complex runtime construction without moving all logic into the composition root.
- Provider, tool, plugin, and memory-protocol isolation guidance for extension-heavy code.
- Testability contracts that require isolated unit tests for extracted behavior and characterization tests before risky moves.
- Dependency-graph and cyclic-reference gates.
- A bundle-system guard skill that can be installed under `codex/skills/bundles`.
- Integration snippets for existing CanDoItAll bundle preparation, execution, bundle validator, and subbundle validator skills.
- Example refactoring playbooks and templates that can be copied into new bundles.

## Recommended install location

Copy the contents of `codex/skills/` into the repository's active skill root:

```text
<repo>/codex/skills/
```

The bundle guard can be installed as:

```text
<repo>/codex/skills/bundles/candoitall-csharp-architecture-bundle-guard/SKILL.md
```

Keep the shared references folder together with the skills:

```text
<repo>/codex/skills/_csharp-architecture-shared/
```

## Core operating rule

For any C# work that touches runtime orchestration, tool catalogs, providers, memory protocols, process drivers, workflow executors, or cross-project references, the bundle must include a C# Architecture Gate before implementation starts.

The gate is not satisfied by saying "use dependency injection" or "split into partial classes." It is satisfied only when the bundle contains:

- a current-state responsibility inventory
- a proposed target boundary map
- a project reference direction map
- a pattern selection record
- a testability plan
- an extraction or construction sequence
- proof requirements that can detect shallow separation
- checkpoints that prevent the old large class from re-accumulating behavior

## External reference anchors

The skills intentionally align with common .NET guidance:

- Microsoft .NET dependency injection guidelines: small, well-factored, easily tested services; avoid direct instantiation of dependent classes inside services; treat many constructor dependencies as a possible responsibility-smell.
- Microsoft .NET unit testing guidance: fast, isolated, repeatable, self-checking tests; unit tests help expose coupling and drive decoupling.
- Microsoft clean architecture guidance: Application Core should not depend on Infrastructure, and UI or composition roots wire infrastructure implementations through interfaces.
- Refactoring.Guru C# pattern catalog: Builder, Factory Method, Abstract Factory, Strategy, Adapter, Facade, Bridge, Decorator, Command, Chain of Responsibility, Observer, and related refactorings are used as vocabulary, not as cargo-cult requirements.

## Package contents

See `SKILL_INDEX.md` for a detailed routing map.
