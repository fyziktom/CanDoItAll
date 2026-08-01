# Capture solution scaffold contract

Record the solution name, app project name, app project directory, test project name, test project directory, target framework, product root, requested .NET archetype, test framework preference, and allowed template switches before any files are created.

Use the grounded product root from project structure or parent artifacts. If no product root is grounded, block unless the parent explicitly defines a managed output root as the product. Do not invent an `external-target/...` path and do not substitute an evidence folder for the product.

Project-structure mindmap values override examples and defaults. If the mindmap names `net10.0`, MSTest, a solution name, an app project name, or a specific feature list, copy those values exactly. Do not add unrequested features or write older framework guidance as a fallback.

Record one target framework as the source of truth for both application and test projects. It is not an application template option, so do not write `--framework` or its value among allowed template switches and do not rely on an installed SDK default. If the selected app and test templates have no common supported target framework, block and return the concrete architecture conflict.

The application and test template fields must use exact one-token `dotnet new` identifiers, not human display names. For example, record `blazorwasm` rather than "Blazor WebAssembly App", and `xunit` rather than "xUnit test project". Put any optional approved app flag only in `application.templateOptions`; do not attach values or inline flags to the template identifier.

For a greenfield .NET app, record the topology supplied by the parent contract or current launch variables:

- solution file location and accepted `.slnx` / `.sln` candidates
- app project directory and project file name
- test project directory and project file name, when tests are in scope

The setup template does not prescribe a `src`/`tests` layout or prohibit an app project at the product root. Select the app template from grounded requirements and the test template from the parent contract or existing repository convention. Block only when a required topology or template choice cannot be determined safely from authoritative current-run context.
