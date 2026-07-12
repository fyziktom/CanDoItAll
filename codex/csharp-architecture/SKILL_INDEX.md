# Skill Index

## Primary coordinator

### `csharp-architecture-governor`

Use first when the work is architecture-heavy, cross-project, refactoring-heavy, or likely to add runtime capabilities, tools, providers, memory modules, workflow/process drivers, dispatchers, builders, factories, or complex composition.

This skill decides which other C# architecture skills must be used and what bundle artifacts must exist before implementation.

## Refactoring and extraction

### `csharp-modular-refactoring`

Use when Codex is tempted to add another partial class, nested helper, broad manager, or catch-all runtime method. It requires responsibility slicing, characterization tests, incremental extraction, and anti-regression proof.

### `csharp-project-boundary-extraction`

Use when a responsibility must move into its own project, when a new contract/implementation split is needed, or when cyclic references are likely.

### `csharp-dependency-graph-audit`

Use when adding a project reference, moving code across projects, introducing a new abstraction, or repairing dependency direction.

## Construction and extension models

### `csharp-factory-builder-composition`

Use when complex runtime objects, process definitions, workflow definitions, tool catalogs, providers, strategies, or options need to be constructed safely.

### `csharp-provider-tool-plugin-isolation`

Use when adding or refactoring tools, provider adapters, plugin surfaces, memory providers, MCP connectors, workflow executors, or runtime capability catalogs.

### `csharp-design-pattern-selection`

Use when choosing among Factory Method, Abstract Factory, Builder, Strategy, Adapter, Facade, Bridge, Decorator, Chain of Responsibility, Command, Observer, State, or Composite.

## Validation and review

### `csharp-testability-contracts`

Use when a change must prove that extracted logic is independently unit-testable and not only covered by broad integration or UI tests.

### `csharp-architecture-review-gate`

Use at bundle checkpoints, before closing critical subbundles, and before merge. It blocks fake separation, partial-class expansion, service-locator shortcuts, cyclic references, and untestable extractions.

## Bundle integration

### `candoitall-csharp-architecture-bundle-guard`

Install under `codex/skills/bundles/`. Use during bundle preparation and execution whenever the bundle touches C# architecture or refactoring.
