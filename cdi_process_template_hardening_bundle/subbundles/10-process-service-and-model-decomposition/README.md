# Process-service and model decomposition

## Purpose
Break up the oversized process service and large model files into focused files by responsibility, excluding auto-generated migration designer code.

## Depends on
09-workspace-decomposition

## Deliverables
- Refactor plan for ProcessesService and companion files
- Focused partials or collaborators for listing, reads, persistence, publication, deletion, runtime, validation, and helpers
- Model splits for definition entities, runtime entities, editor DTOs, and view models

## Repository touchpoints
- `src/CanDoItAll.Modules.Processes/ProcessesService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Reads.cs`
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplatePackModels.cs`

## Validation commands or checks
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~ProcessesService`
- `dotnet test tests/CanDoItAll.Mcp.Processes.Tests/CanDoItAll.Mcp.Processes.Tests.csproj`

## Senior review questions
- Did the service split align with real responsibilities rather than arbitrary file size?
- Are read models, runtime entities, and template-pack models easier to evolve independently?
- Did the split preserve existing process and MCP tool behavior?

## Strict corrective rule
Create a service/model corrective subbundle before continuing to the final regression phase.
