---
name: candoitall-csharp-architecture-bundle-guard
description: "Add strict C# architecture gates to CanDoItAll bundles. Use during bundle preparation, execution, validation, or repair when work touches C# refactoring, large classes, partial classes, tools, providers, memory protocols, process drivers, runtime composition, project references, factories, builders, or testability."
---

# CanDoItAll C# Architecture Bundle Guard

Use this as the bundle-level architecture guard for C# work.

This skill extends the normal bundle workflow. It does not replace bundle preparation, execution, or validation. It adds C# architecture artifacts and checkpoints so implementation cannot drift into partial-class expansion or fake separation.

Use `candoitall-codeanalytics-mcp` as the default read-only evidence source when it is available. The bundle should record snapshot id, dashboard health, relevant findings or hotspots, dependency/cycle result, and exact source symbols for the architecture gate.

## Activation signals

Activate this guard when a bundle includes:

- large class refactoring
- partial class changes
- runtime orchestration changes
- new tool or tool family
- new provider or provider family
- memory provider or memory protocol changes
- process driver changes
- workflow executor changes
- new factory, builder, catalog, adapter, facade, strategy, or pipeline
- project reference changes
- cyclic reference risk
- testability improvement
- split into new project

## Required preparation additions

During bundle preparation, add these files:

```text
architecture/00-csharp-current-state-inventory.md
architecture/01-csharp-boundary-map.md
architecture/02-csharp-dependency-direction.md
architecture/03-csharp-pattern-selection-records.md
architecture/04-csharp-testability-plan.md
plan/architecture-checkpoints.md
reviews/csharp-architecture-gate.md
```

Use the shared reference `bundle-architecture-sections.md` for required content. In the repo copy, read `../../_csharp-architecture-shared/references/bundle-architecture-sections.md`; in the flattened global Codex install, read `../_csharp-architecture-shared/references/bundle-architecture-sections.md`.

The current-state inventory and dependency-direction map must cite CodeAnalytics evidence when the MCP is available. If it is unavailable, record the validation gap explicitly.

## Required subbundle additions

Every architecture-relevant subbundle README must include:

```markdown
## C# Architecture Impact

## Boundary Ownership

## Dependency Direction

## Pattern Decision

## Testability Contract

## Partial Class Policy

## Architecture Proof Required
```

For critical foundation subbundles, also require:

- before/after dependency direction proof
- CodeAnalytics snapshot, findings, and dependency/cycle proof when available
- source assertion that moved behavior left the old class
- direct unit tests for extracted behavior
- negative test for shallow implementation
- no-new-partial or justified-temporary-partial proof
- old-class shrink or thin-facade proof
- downstream unlock decision

## Phase planning rule

Split architecture-heavy bundles into phases such as:

1. Inventory and characterization.
2. Contract and boundary extraction.
3. Implementation extraction.
4. Factory/builder/catalog composition.
5. Runtime wiring and old-code deletion.
6. Independent unit tests and composition smoke.
7. Architecture checkpoint review.
8. Dependent feature work.

Do not put dependent feature work before the boundary and testability foundation is proven.

## Closure gate

Before closing an architecture subbundle, run `csharp-architecture-review-gate`.

The closure is blocked if:

- another partial file was added as the final boundary
- the old runtime still owns the extracted responsibility
- project references point inward incorrectly
- tests do not instantiate extracted behavior directly
- the pattern selection record is missing
- a new extension still requires editing the old monolith

## Bundle repair trigger

Repair the bundle before continuing if execution discovers:

- a required contract project was not planned
- a cyclic reference appears
- the bundle split work by files instead of responsibilities
- a planned pattern is not justified
- tests cannot be written without full runtime construction
- old class shrink proof was not planned

## Exit condition

The bundle can proceed only when the architecture artifacts, subbundle checkpoints, proof requirements, and dependency map make fake separation visible and blockable.
