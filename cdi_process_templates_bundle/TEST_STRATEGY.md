# Test strategy

## Scope
The revised bundle strengthens coverage for:
- pack loading
- current-module import projection
- Mermaid export and supporting file inventory
- current baseline process parity
- regression validation of dependencies and artifact inputs
- shared/local resource integrity

## Prepared tests
- `ProcessTemplatePackLoaderTests`
- `ProcessTemplateProjectionServiceTests`
- `ProcessTemplateCatalogServiceTests`
- `ProcessTemplateMermaidExporterTests`
- `CurrentArchitectureTemplateParityTests`
- existing MCP tool tests retained from the earlier bundle

## External execution note
The tests are prepared in the bundle overlay, but `dotnet` SDK was not available in this container, so the xUnit test suite could not be run here. The Python validator was run successfully and returned zero errors.
