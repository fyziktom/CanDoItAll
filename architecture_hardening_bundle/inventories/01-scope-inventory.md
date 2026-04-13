# Scope inventory

## Definitions and authoring core

- `src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEditorModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasBranching.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Support.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Publication.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessStepEditorForm.razor`

## Runtime and read side

- `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.Helpers.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Reads.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeEntityConfigurations.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeViewModels.cs`

## UI and canvas

- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Persistence.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Links.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Actions.cs`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.cs`
- related `ProcessCanvasSurfaceFactory.*.cs`

## Template subsystem

- `src/CanDoItAll.Modules.Processes/ProcessTemplatePackLoader.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateCatalogService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateLibraryService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessDevelopmentSeedService.RuntimeSeeds.Complex.cs`

## Cross-module duplication candidates

- `src/CanDoItAll.Modules.Factory/PromptLibraryPackLoader.cs`
- `src/CanDoItAll.Modules.Prompts/Pages/PromptGalleryPage.razor`
- `src/CanDoItAll.Modules.Projects/ProjectModels.cs`
- `src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`

## Tests in scope

### Integration
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessImportMetadataIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessDeletionIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/SqliteWriteCoordinationIntegrationTests.cs`

### Components
- `tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`
- `tests/CanDoItAll.Tests.Components/ProcessCanvasSurfaceFactoryTests.cs`
- `tests/CanDoItAll.Tests.Components/ProcessStepEditorFormTests.cs`

### MCP
- `tests/CanDoItAll.Mcp.Processes.Tests/ProcessesToolsTests.cs`
- `tests/CanDoItAll.Mcp.Processes.Tests/ProcessTemplateProjectionServiceTests.cs`
- `tests/CanDoItAll.Mcp.Processes.Tests/ProcessTemplateCatalogServiceTests.cs`
- `tests/CanDoItAll.Mcp.Processes.Tests/ProcessTemplatePackLoaderTests.cs`
- `tests/CanDoItAll.Mcp.Processes.Tests/ProcessTemplateMermaidExporterTests.cs`

## Migration providers in scope

- `src/CanDoItAll.Migrations.Sqlite/*`
- `src/CanDoItAll.Migrations.PostgreSql/*`
