# Validation report

## What was validated in this container
- Audited the current repository against the in-repo previous apply manifest.
- Confirmed **477** missing targets out of **501**, overwhelmingly the real template-pack files.
- Confirmed the current repository differs from the older overlay in **5** overlapping non-pack files, so those newer repository versions were preserved.
- Materialized the full template-pack tree into the new bundle overlay.
- Added focused test files and audit helpers.
- Rebuilt the workbook catalog and supporting CSV exports.
- Ran the process-template pack validator against the new bundle overlay.

## Pack-validator result
- Process count: **9**
- Step count: **54**
- Dependency count: **52**
- Artifact input count: **20**
- Baseline scenario count: **5**
- Error count: **0**

## Current-repository audit result against the older in-repo manifest
- Manifest entry count: **501**
- Missing target count: **477**
- Result: **not yet applied in the current repository**

## What was not validated here
- `dotnet build`
- `dotnet test`
- Browser or Playwright execution

## Required post-apply commands in a dotnet-capable environment
- `python cdi_process_template_completion_and_architecture_hardening_bundle/tools/validate_process_template_pack.py output/process-template-pack`
- `python tools/audit_process_template_bundle_materialization.py . cdi_process_templates_bundle/apply-manifest.json`
- `dotnet build CanDoItAll.slnx -v:minimal`
- `dotnet test tests/CanDoItAll.Mcp.Processes.Tests/CanDoItAll.Mcp.Processes.Tests.csproj -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessImportMetadataIntegrationTests|FullyQualifiedName~SqliteWriteCoordinationIntegrationTests" -v:minimal`

## Honesty note
This validation report is intentionally explicit: the bundle content itself was validated, but the repository has **not** been claimed as fully remediated in this container.
