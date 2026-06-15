# Classify .NET application type and project boundary

Read the project structure, repository files, requested work, and upstream scope to classify the .NET target as backend-only API/service, Blazor Server/SSR, Blazor WebAssembly, Blazor WASM PWA, worker, console app, class library, or mixed solution. Record product root, test root, runnable projects, UI routes if present, and any contradictions before design starts. Do not create, edit, build, test, or run product files in this step.

## Contract
- Inputs: Scope packet, project-structure node, repository context, and requested .NET deliverable.
- Outputs: Typed .NET application classification with product root, test root, runtime surfaces, and UI/no-UI applicability.
- Evidence: Project context, app type, route/runtime inventory, contradictions, and assumptions.
- Operation target scope: `ExternalProductTargetReadOnly`
