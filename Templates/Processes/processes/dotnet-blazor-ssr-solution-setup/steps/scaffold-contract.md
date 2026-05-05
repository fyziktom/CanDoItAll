# Capture solution scaffold contract

Record the solution name, app project name, app project directory, test project name, test project directory, target framework, product root, and allowed template switches before any files are created.

If the current run has a grounded external product root, use that exact root. If no external root is grounded, use the dispatcher-provided current-run managed output root as the product root and do not invent an `external-target/...` path.

For a greenfield Blazor SSR app, use this layout unless the parent contract explicitly says otherwise:

- solution file at the product root as `<SolutionName>.slnx` or `<SolutionName>.sln`
- app project under `<ProductRoot>/src/<AppProjectName>`
- test project under `<ProductRoot>/tests/<TestProjectName>`

Do not use the product root itself as the app project parent after creating the solution file. A solution file and a project directory may share the same base name, but the project directory still belongs under `src/`.
