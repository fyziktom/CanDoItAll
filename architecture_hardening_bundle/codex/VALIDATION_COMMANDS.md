# Validation commands

## Prepared-stage validator

```text
python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\architecture_hardening_bundle --profile initiative --stage prepared
```

## Build

```text
dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -v:minimal
```

## Integration tests

```text
dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessImportMetadataIntegrationTests|FullyQualifiedName~ProcessDeletionIntegrationTests|FullyQualifiedName~SqliteWriteCoordinationIntegrationTests" -v:minimal
```

## Component tests

```text
dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal
```

## MCP process tests

```text
dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --filter "FullyQualifiedName~ProcessesToolsTests|FullyQualifiedName~ProcessTemplateProjectionServiceTests|FullyQualifiedName~ProcessTemplateCatalogServiceTests|FullyQualifiedName~ProcessTemplatePackLoaderTests|FullyQualifiedName~ProcessTemplateMermaidExporterTests" -v:minimal
```

## Completed-stage validator

```text
python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\architecture_hardening_bundle --profile initiative --stage completed
```
