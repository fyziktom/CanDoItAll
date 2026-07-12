# Classify .NET application type and project boundary

Read the project structure, repository files, requested work, and upstream scope to classify the .NET target as backend-only API/service, Blazor Server/SSR, Blazor WebAssembly, Blazor WASM PWA, worker, console app, class library, or mixed solution. Record product root, test root, runnable projects, UI routes if present, and any contradictions before design starts. Do not create, edit, build, test, or run product files in this step.

## Contract
- Inputs: Scope packet, project-structure node, repository context, and requested .NET deliverable.
- Outputs: Typed .NET application classification with product root, test root, runtime surfaces, and UI/no-UI applicability.
- Evidence: Project context, app type, route/runtime inventory, contradictions, and assumptions.
- Operation target scope: `ExternalProductTargetReadOnly`

## Greenfield Classification
- When `ProductRoot`, `OutputRoot`, or `ExternalTargetRoot` is grounded by project structure and that root is missing or empty, treat the absence of `.sln`, `.slnx`, or `.csproj` files as greenfield repository state, not by itself as a blocker.
- Classify the intended app type only from the project structure, repository evidence, scope packet, and requested deliverable. The later slice architecture step owns the concrete bootstrap decision for a writable product root; this read-only root review must not synthesize template, framework, test-framework, or layout launch variables.
- Cite stable project-structure node ids, artifact refs, titles, or current-run workspace tool receipts as source evidence. Do not cite source document paths, native absolute paths, workspace-relative `.slnx`, `.sln`, `.csproj`, markdown, project-media paths, managed-files paths, scoped storage paths, tool-runs paths, or `SourceDocLink` values in the artifact body, reason, summary, next actions, or `evidenceRefs`.
- Block only when the product root, app type, or source-of-truth ownership is missing, contradictory, or unsafe to determine from the available project structure and upstream artifacts.
