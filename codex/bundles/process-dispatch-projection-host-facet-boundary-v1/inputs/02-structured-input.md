# Structured Input

## Objectives

- Continue dispatcher isolation by narrowing projection dependencies into module-local facets.
- Preserve runtime projection behavior, source-family ordering, artifact identity, lineage, storage placement, and candidate mutation semantics.
- Keep future driver-readiness work documentation-only in this bundle.

## Hard Constraints

- Do not create `CanDoItAll.Processes.Core`.
- Do not introduce production process-driver APIs or registries.
- Do not move EF entities, public contracts, or DB writes into a new project.
- Do not touch UI, Razor, CSS, JavaScript, or TypeScript files.

## Validation Expectations

- Build and focused projection tests must pass.
- Source scans must prove no Process Core, production driver API, UI drift, TODO, stub, `NotImplemented`, or fixture-specific shortcut was introduced.
- Critical gates must produce artifact-backed proof under `bundle://proof/SBxx/`.

