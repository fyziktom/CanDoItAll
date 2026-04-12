# Validation report

## Execution status
Repository execution is complete for the scope of this bundle.

## Commands executed
- `python cdi_process_template_hardening_bundle/tools/audit_bundle_application.py . cdi_process_templates_bundle/apply-manifest.json`
  - Result: **ok=true**, **501** targets checked, **0** missing.
- `python cdi_process_template_hardening_bundle/tools/validate_process_template_pack.py output/process-template-pack`
  - Result: **9** processes, **58** steps, **56** dependencies, **28** artifact inputs, **5** baseline scenarios, **0** errors.
- `dotnet build CanDoItAll.slnx -v:minimal`
  - Result: passed.
  - Visible warnings: pre-existing `NU1510` warnings in `src/CanDoItAll.Mcp.DotNetWatch/CanDoItAll.Mcp.DotNetWatch.csproj` and pre-existing `ASP0006` warnings in `tests/CanDoItAll.Tests.Components/TabsComponentTests.cs`.
- `dotnet test tests/CanDoItAll.Mcp.Processes.Tests/CanDoItAll.Mcp.Processes.Tests.csproj --no-build -v:minimal`
  - Result: **20 passed**.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessImportMetadataIntegrationTests|FullyQualifiedName~SqliteWriteCoordinationIntegrationTests|FullyQualifiedName~SeedBaselineAsync_supports_global_then_project_scoped_baselines_without_slug_collisions" -v:minimal`
  - Result: **5 passed**.
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-build --filter "FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessWorkspaceTests" -v:minimal`
  - Result: **12 passed**.

## Corrective findings closed during execution
- Replaced invalid branching merge topology with route-specific merge gates in `branching-code-review`.
- Corrected stale hotfix and software-delivery baseline scenarios so they match the current runtime contracts and still exercise failure or blocked paths.
- Corrected local resource owner and audience role drift in `hotfix-rollout` and `incident-response`.
- Hardened the pack validator to catch:
  - invalid local resource role ownership
  - stale baseline assignment role keys
  - stale baseline step keys
  - stale baseline branch references
  - stale baseline artifact step references

## What was not rerun for this bundle
- No new Playwright/browser proof run.
- No full-solution test sweep beyond the build and targeted regression slices above.

## Closure statement
The bundle no longer describes a future corrective run. The repository state, validator output, and .NET proof above are the actual closure evidence for this execution.
