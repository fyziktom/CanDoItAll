# CanDoItAll process-template completion and architecture-hardening bundle

## Status
This bundle has now been executed in the repository and closed with validation evidence.

## Historical reason this bundle existed
The earlier in-repo completion narrative was not trustworthy because the repository was still missing the actual file-driven template-pack folders and several architecture-hardening tasks had only been described, not executed.

## What this execution completed
- Materialized and audited the full file-driven template pack in `output/process-template-pack/`.
- Corrected pack drift in sidecars, local-resource role ownership, and baseline seed scenarios.
- Hardened loader and DI behavior around the process template pack.
- Hardened SQLite-sensitive write paths and added regression coverage for them.
- Decomposed the oversized process-module files into smaller responsibility-focused files.
- Strengthened validator coverage so baseline and pack drift fail in validation instead of at runtime.

## Final proof
- Old-manifest application audit: **501** targets checked, **0** missing.
- Pack validator: **9** processes, **58** steps, **56** dependencies, **28** artifact inputs, **5** baseline scenarios, **0** errors.
- `dotnet build CanDoItAll.slnx -v:minimal`: passed.
- `dotnet test tests/CanDoItAll.Mcp.Processes.Tests/CanDoItAll.Mcp.Processes.Tests.csproj --no-build -v:minimal`: **20 passed**.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessImportMetadataIntegrationTests|FullyQualifiedName~SqliteWriteCoordinationIntegrationTests|FullyQualifiedName~SeedBaselineAsync_supports_global_then_project_scoped_baselines_without_slug_collisions" -v:minimal`: **5 passed**.
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-build --filter "FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessWorkspaceTests" -v:minimal`: **12 passed**.

## Still visible after closure
- `CanDoItAll.Mcp.DotNetWatch.csproj` still emits pre-existing `NU1510` warnings during solution build.
- `tests/CanDoItAll.Tests.Components/TabsComponentTests.cs` still emits pre-existing `ASP0006` warnings during solution build.
- This bundle did not require a fresh Playwright/browser rerun; closure was based on pack, build, MCP, integration, and component proof.
