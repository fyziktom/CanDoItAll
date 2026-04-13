# Proof contract

## Prepared-stage proof required before implementation

Run on the target machine:

```text
python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\cdi_process_module_architecture_hardening_bundle --profile initiative --stage prepared
```

If the validator fails, repair the bundle before code work begins.

## Core build proof

```text
dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -v:minimal
```

## Focused .NET proof matrix

### Integration
```text
dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessImportMetadataIntegrationTests|FullyQualifiedName~ProcessDeletionIntegrationTests|FullyQualifiedName~SqliteWriteCoordinationIntegrationTests" -v:minimal
```

### Components
```text
dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal
```

### MCP processes
```text
dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --filter "FullyQualifiedName~ProcessesToolsTests|FullyQualifiedName~ProcessTemplateProjectionServiceTests|FullyQualifiedName~ProcessTemplateCatalogServiceTests|FullyQualifiedName~ProcessTemplatePackLoaderTests|FullyQualifiedName~ProcessTemplateMermaidExporterTests" -v:minimal
```

## Browser proof required

At minimum, capture a real Playwright/browser pass for `/processes` with:
- large desktop viewport first,
- a narrower-width pass second,
- screenshots reviewed against readability, overlap, clipping, spacing, alignment, and hierarchy questions.

## Completed-stage proof

```text
python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\cdi_process_module_architecture_hardening_bundle --profile initiative --stage completed
```

## Proof integrity rules

- Do not reconstruct proof from memory.
- Do not mark a gate passed if its proof is missing.
- Do not substitute reasoning for browser proof on UI-relevant changes.
- If proof exposes a weak foundation, reopen the earlier subbundle instead of hiding the problem.
