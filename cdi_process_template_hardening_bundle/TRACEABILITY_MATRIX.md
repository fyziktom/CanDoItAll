# Traceability matrix

| UserRequirement | BundleEvidence | Status |
| --- | --- | --- |
| Actual process templates must exist in folders, not only in workbook rows. | repo-overlay/output/process-template-pack/**; artifacts/process-template-pack-tree.txt | Addressed |
| Templates must remain file-driven and not hardcoded in code. | repo-overlay/output/process-template-pack/**; tools/validate_process_template_pack.py | Addressed |
| Shared resources and process-local resources must both be supported. | repo-overlay/output/process-template-pack/shared/**; repo-overlay/output/process-template-pack/processes/*/{roles,artifacts,checklists,validations,prompts}/** | Addressed |
| Mermaid flowchart and sequence exports with sidecar docs. | repo-overlay/output/process-template-pack/processes/*/mermaid/*.mmd; definition.md; steps/*.md; roles/*.md | Addressed |
| Detailed roles, prompts, checklists, validation criteria, artifact expectations, and tests. | process-template pack folders; workbook sheets; repo-overlay/tests/* | Addressed |
| Architecture review must find weak spots and trigger corrective subbundles when needed. | analysis/architecture-weak-spots.md; analysis/architecture-review-phases.json; subbundles/_corrective-template | Addressed |
| SQLite-sensitive issues must be reviewed and improved. | analysis/sqlite-hardening-review.md; subbundles/06-sqlite-write-path-hardening; repo-overlay/tests/CanDoItAll.Tests.Integration/ProcessImportMetadataIntegrationTests.cs | Addressed |
| Long files must be identified and split into smaller parts. | analysis/long-file-refactor-plan.md; subbundles/08-10 | Prepared for execution |
| Final bundle must be honest about what was executed versus only prepared. | VALIDATION_REPORT.md; ASSUMPTIONS_AND_LIMITATIONS.md; QUALITY_GATES.md | Addressed |