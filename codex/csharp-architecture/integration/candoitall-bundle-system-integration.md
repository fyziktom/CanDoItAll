# CanDoItAll Bundle System Integration

This package is designed to fit the existing CanDoItAll bundle workflow.

The existing bundle workflow already requires raw input preservation, dependency-aware subbundles, proof manifests, semantic invariants, gate results, and final closure. The missing piece for C# architecture-heavy work is a hard architecture guard that prevents implementation from satisfying a broad requirement by adding partial classes, nested helper types, or cosmetic interfaces.

## Integration objective

When a bundle touches C# architecture, the bundle system should require:

- current-state architecture inventory
- target project/type boundary map
- project reference direction map
- pattern selection records
- testability plan
- explicit partial-class policy
- architecture checkpoints after foundation subbundles
- source and test proof that extracted responsibilities really moved

## Install locations

Copy these skill folders:

```text
codex/skills/csharp-architecture-governor
codex/skills/csharp-modular-refactoring
codex/skills/csharp-project-boundary-extraction
codex/skills/csharp-factory-builder-composition
codex/skills/csharp-provider-tool-plugin-isolation
codex/skills/csharp-testability-contracts
codex/skills/csharp-dependency-graph-audit
codex/skills/csharp-design-pattern-selection
codex/skills/csharp-architecture-review-gate
codex/skills/_csharp-architecture-shared
```

Copy the bundle guard:

```text
codex/skills/bundles/candoitall-csharp-architecture-bundle-guard
```

## Recommended edits to existing bundle skills

Apply the snippets in:

```text
integration/append-to-bundle-preparation.md
integration/append-to-bundle-execution.md
integration/append-to-bundle-validator.md
integration/append-to-subbundle-validator.md
```

These snippets do not replace the existing bundle discipline. They add C# architecture-specific gates.

## Recommended bundle profile

For architecture-heavy tasks, use the existing initiative profile and add the architecture files listed in `integration/architecture-bundle-profile.md`.

## Recommended checkpoint cadence

After every critical foundation subbundle that changes contracts, project references, factories, builders, provider catalogs, memory protocols, or runtime wiring:

1. Run the subbundle validator.
2. Run `csharp-architecture-review-gate`.
3. Update `reviews/csharp-architecture-gate.md`.
4. Record before/after project references.
5. Record source assertions.
6. Decide whether dependent subbundles may start.

## Expected result

Codex should stop treating "split it" as "add another partial." Instead, it should produce:

- smaller cohesive types
- real project boundaries when justified
- factories/builders/catalogs where construction and extension require them
- independently testable behavior
- safer dependency direction
- durable proof that the design improved
