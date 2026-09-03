# C# Dependency Direction

## Current graph

Complete principal-owner csproj references are in [project-references.json](../analysis/project-references.json). Relevant allowed edges:

- Maf → Core, Providers, Runtime.Abstractions, Models, Tooling, existing capability/skill/MCP adapters.
- Core → Runtime.Abstractions, Models, Providers and existing capability/history/infrastructure abstractions.
- Runtime.Abstractions → Models and ProviderHistory.Abstractions.
- Providers → Models, ProviderPipelines and ProviderHistory.Abstractions.
- Workbench → Core, Models, Tooling and its current domain/UI modules.
- SharedProviders.Http → SharedProviders.Abstractions, Models, Providers.
- Web/composition → application owners and concrete adapters.

## Target invariants

No new project reference is expected. Models and Runtime.Abstractions must stay free of Microsoft.Agents.AI, Microsoft.Extensions.AI, OllamaSharp and OpenAI SDK contracts. SDK translation remains in Maf. Core must not reference Maf, Workbench, Web or SharedProviders.Http. Provider transport code must not reference project-structure mutation services.

Project and module ownership must be checked independently: the existing Workbench module/type cycles are not a model for new code. A file move within the same partial type does not establish a dependency boundary.

## Verification

At each changed architecture checkpoint, compare relevant csproj files with baseline and run CodeAnalytics build/dependencies for the changed owner scope plus callers. Record scope, diagnostics and cycles; investigate new cycles. If tooling is unavailable, inspect project references and source type dependencies explicitly and record the limitation. Re-run affected builds; no source-level reference claim substitutes for compilation.

Reject a patch that resolves a compiler error by adding a forbidden reference, moving neutral contracts into a UI/module assembly, or passing an untyped universal service bag.
