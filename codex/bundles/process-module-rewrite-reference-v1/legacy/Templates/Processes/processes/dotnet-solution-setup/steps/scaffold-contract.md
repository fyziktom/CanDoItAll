# Capture solution scaffold contract

Record the solution name, app project name, app project directory, test project name, test project directory, target framework, product root, requested .NET archetype, test framework preference, and allowed template switches before any files are created.

Use the grounded product root from project structure or parent artifacts. If no product root is grounded, block unless the parent explicitly defines a managed output root as the product. Do not invent an `external-target/...` path and do not substitute an evidence folder for the product.

Project-structure mindmap values override examples and defaults. If the mindmap names `net10.0`, MSTest, a solution name, an app project name, or a specific feature list, copy those values exactly. Do not add unrequested features or write older framework guidance as a fallback.

For a greenfield .NET app, use this layout unless the parent contract explicitly says otherwise:

- solution file at the product root as `<SolutionName>.slnx` or `<SolutionName>.sln`
- app project under `<ProductRoot>/src/<AppProjectName>`
- test project under `<ProductRoot>/tests/<TestProjectName>`

Select the app template from grounded requirements. Select the test template from the parent contract or existing repository convention. Escalate when those choices cannot be made without guessing.

Do not use the product root itself as the app project parent after creating the solution file. A solution file and a project directory may share the same base name, but the project directory still belongs under `src/`.
