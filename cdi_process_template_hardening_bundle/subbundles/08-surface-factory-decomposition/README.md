# Surface-factory decomposition

## Purpose
Split the oversized canvas-surface factory into coherent partials or collaborators so node creation, links, ports, chrome, color rules, and coordinate resolution become maintainable.

## Depends on
07-architecture-review-gate-b

## Deliverables
- Refactor plan and implementation tasks for ProcessCanvasSurfaceFactory
- Smaller files grouped by responsibility
- Regression coverage for definition/run surface output parity

## Repository touchpoints
- `src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.cs`
- `tests/CanDoItAll.Tests.Components/ProcessCanvasSurfaceFactoryTests.cs`

## Validation commands or checks
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter FullyQualifiedName~ProcessCanvasSurfaceFactoryTests`

## Senior review questions
- Did the split preserve exact canvas behavior?
- Are chrome, ports, links, and coordinate logic now isolated enough to evolve safely?
- Did the refactor remove hardcoded assumptions or merely move them?

## Strict corrective rule
Create a decomposition corrective subbundle and rerun the component tests before continuing.
