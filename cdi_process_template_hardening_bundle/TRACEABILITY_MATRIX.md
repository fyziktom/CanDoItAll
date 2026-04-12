# Traceability matrix

| UserRequirement | BundleEvidence | Status |
| --- | --- | --- |
| Actual process templates must exist in folders, not only in workbook rows. | `output/process-template-pack/**`; `cdi_process_template_hardening_bundle/tools/audit_bundle_application.py`; `VALIDATION_REPORT.md` | Addressed |
| Templates must remain file-driven and not hardcoded in code. | `output/process-template-pack/**`; `src/CanDoItAll.Modules.Processes/ProcessTemplatePackLoader.cs`; `cdi_process_template_hardening_bundle/tools/validate_process_template_pack.py` | Addressed |
| Shared resources and process-local resources must both be supported. | `output/process-template-pack/shared/**`; `output/process-template-pack/processes/*/{roles,artifacts,checklists,validations,prompts}/**`; `tests/CanDoItAll.Mcp.Processes.Tests/*` | Addressed |
| Mermaid flowchart and sequence exports with sidecar docs must be present. | `output/process-template-pack/processes/*/mermaid/*.mmd`; `output/process-template-pack/processes/*/definition.md`; `output/process-template-pack/processes/*/steps/*.md` | Addressed |
| Baseline scenarios must seed without stale role, step, or branch drift. | `output/process-template-pack/seed-catalog/baseline-scenarios.json`; `src/CanDoItAll.Modules.Processes/ProcessDevelopmentSeedService.*`; `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` | Addressed |
| Validator coverage must catch pack and baseline drift before runtime seeding. | `cdi_process_template_hardening_bundle/tools/validate_process_template_pack.py` | Addressed |
| Loader and DI behavior must be explicit and testable. | `src/CanDoItAll.Modules.Processes/ProcessTemplatePackLoader.cs`; `src/CanDoItAll.Modules.Processes/ProcessesModuleServiceCollectionExtensions.cs`; `tests/CanDoItAll.Mcp.Processes.Tests/*` | Addressed |
| SQLite-sensitive issues must be reviewed and improved. | `src/CanDoItAll.Infrastructure/Persistence/SqliteWriteCoordination.cs`; `src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`; `tests/CanDoItAll.Tests.Integration/SqliteWriteCoordinationIntegrationTests.cs` | Addressed |
| Long files must be identified and split into smaller parts. | `analysis/long-file-refactor-plan.md`; `src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.*.cs`; `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.*.cs`; `src/CanDoItAll.Modules.Processes/ProcessesService.*.cs` | Addressed |
| Final bundle must be honest about what was executed versus only prepared. | `README.md`; `EXECUTIVE_SUMMARY.md`; `VALIDATION_REPORT.md`; `analysis/final-qa-architect-inspection.md`; `validation-result.json` | Addressed |
