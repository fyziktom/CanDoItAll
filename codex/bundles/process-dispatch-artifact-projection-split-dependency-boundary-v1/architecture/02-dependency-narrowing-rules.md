# Dependency Narrowing Rules

## Rule 1: Do not pass the entire dispatcher into projection coordinators

The previous bundle used nested coordinators and `dispatchService` references as a transitional shortcut. This bundle must replace that with explicit module-local dependencies.

## Rule 2: Separate pure decisions from side effects

Pure helpers may decide:

- whether an artifact is eligible,
- which expectation matches,
- which path should be used,
- which projection plan should be produced.

Side-effect coordinators may perform:

- `File.ReadAllBytesAsync`,
- `File.Copy`,
- `Directory.CreateDirectory`,
- storage placement,
- `RecordArtifactAsync`,
- candidate state mutation.

Do not hide side effects in classes named `Rules`, `Planner`, or `Resolver`.

## Rule 3: Keep source-family order stable

The orchestrator must call source families in the existing order. Tests and source assertions must check it.

## Rule 4: Keep everything internal and module-local

No public API surface. No Core. No driver API.

## Rule 5: Create a temporary compatibility layer only when required

Temporary wrappers are allowed, but every wrapper must have:
- an explicit owner subbundle,
- a removal or stabilization subbundle,
- a source scan proving it is not growing.
