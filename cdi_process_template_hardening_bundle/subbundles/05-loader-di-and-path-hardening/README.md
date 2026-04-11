# Loader, DI, and pack-path hardening

## Purpose
Harden the template-pack loading path so it is explicit, testable, and aligned with dependency injection instead of hidden static construction paths.

## Depends on
04-architecture-review-gate-a

## Deliverables
- Plan and implementation tasks for replacing static pack loading shortcuts
- Explicit pack-root configuration strategy
- Regression tests for loader resolution and pack-root overrides

## Repository touchpoints
- `src/CanDoItAll.Modules.Processes/ProcessCanvasTemplateCatalog.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplatePackLoader.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesModuleServiceCollectionExtensions.cs`

## Validation commands or checks
- `dotnet test tests/CanDoItAll.Mcp.Processes.Tests/CanDoItAll.Mcp.Processes.Tests.csproj --filter FullyQualifiedName~ProcessTemplatePack`

## Senior review questions
- Can the template pack be resolved without hidden static state?
- Can tests override the pack root cleanly?
- Does the DI graph own the loader lifecycle?

## Strict corrective rule
Create a loader-hardening corrective subbundle and stop before SQLite or refactor work continues.
