# Classify .NET application type and project boundary

Read the project structure, repository files, requested work, and upstream scope to classify the .NET target as backend-only API/service, Blazor Server/SSR, Blazor WebAssembly, Blazor WASM PWA, worker, console app, class library, or mixed solution. Record product root, test root, runnable projects, UI routes if present, and any contradictions before design starts. Do not create, edit, build, test, or run product files in this step.

## Contract
- Inputs: Scope packet, project-structure node, repository context, and requested .NET deliverable.
- Outputs: Typed .NET application classification with product root, test root, runtime surfaces, and UI/no-UI applicability.
- Evidence: Project context, app type, route/runtime inventory, contradictions, and assumptions.
- Operation target scope: `ExternalProductTargetReadOnly`

## Greenfield Classification
- When `ProductRoot`, `OutputRoot`, or `ExternalTargetRoot` is grounded by project structure and that root is missing or empty, treat the absence of `.sln`, `.slnx`, or `.csproj` files as greenfield repository state, not by itself as a blocker.
- If `DotNetScaffoldContract`, `DotNetAppArchetype`, or `ProjectStructureContextSummary` identifies the intended app type and layout, classify the target from those typed launch facts and record that implementation must scaffold later in a product-mutable setup step.
- When classifying from `ProjectStructureContextSummary` or `DotNet*` launch variables, cite those launch variable names as source evidence. Do not cite source document paths, native absolute paths, workspace-relative `.slnx`, `.sln`, `.csproj`, markdown, project-media paths, managed-files paths, scoped storage paths, tool-runs paths, or `SourceDocLink` values in the artifact body, reason, summary, next actions, or `evidenceRefs`, even when those path-like values appear in the current step brief, source-document metadata, launch variables, retry diagnostics, or project-structure context. If a source document must be named, cite its stable document id, project-structure node id, title, or current-run workspace tool receipt instead.
- Block only when the product root, app type, or source-of-truth ownership is missing, contradictory, or unsafe to infer from project structure and launch variables.
