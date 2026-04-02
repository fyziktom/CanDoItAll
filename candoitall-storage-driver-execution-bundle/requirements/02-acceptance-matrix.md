
# Acceptance Matrix

| Requirement | Owning phase | Primary proof | Do not close until |
| --- | --- | --- | --- |
| RQ-001 | 01-phase-01-models-interfaces-and-persistence-contracts | Build + migrations + unit proof | Domain models + provider interface + capability model exist and drivers can register by kind. |
| RQ-002 | 01-phase-01-models-interfaces-and-persistence-contracts | Build + migrations + unit proof | New entities and migrations exist in both migration projects and bootstrap/seed upgrade path is documented. |
| RQ-003 | 01-phase-01-models-interfaces-and-persistence-contracts | Build + migrations + unit proof | Compatibility adapter exists and legacy callers can be migrated incrementally. |
| RQ-004 | 01-phase-01-models-interfaces-and-persistence-contracts | Build + migrations + unit proof | Routing contracts, recommendation context, and default policy matrix are implemented and testable. |
| RQ-005 | 02-phase-02-provider-services-routing-and-batch-pipeline | Build + unit/integration proof | Registry resolves concrete drivers, connection test path exists, and runtime services are DI-registered. |
| RQ-006 | 02-phase-02-provider-services-routing-and-batch-pipeline | Build + unit/integration proof | Provider folders/classes exist, each driver meets the shared contract, and no module depends on provider-specific classes directly. |
| RQ-007 | 02-phase-02-provider-services-routing-and-batch-pipeline | Build + unit/integration proof | Transfer manifest + progress + bounded concurrency pipeline exist and at least one folder migration path is covered by tests. |
| RQ-008 | 02-phase-02-provider-services-routing-and-batch-pipeline | Build + unit/integration proof | Unified access route/service exists, local-open is capability-gated, and preview/download actions no longer assume /managed-files paths only. |
| RQ-009 | 04-phase-04-cross-project-adoption-ui-and-validation | Playwright MCP screenshots + execution report + QA audit | Reusable components exist in a shared component location and are used by multiple pages. |
| RQ-010 | 04-phase-04-cross-project-adoption-ui-and-validation | Playwright MCP screenshots + execution report + QA audit | Settings page exposes storage tab, wizard, test connection flow, and management list/detail interactions. |
| RQ-011 | 04-phase-04-cross-project-adoption-ui-and-validation | Playwright MCP screenshots + execution report + QA audit | Users can create a storage node, link it to a storage record, and see/update the reference from project structure. |
| RQ-012 | 04-phase-04-cross-project-adoption-ui-and-validation | Playwright MCP screenshots + execution report + QA audit | All in-scope touchpoints from the XLSX inventory have implementation ownership, code changes, and proof. |
| RQ-013 | 03-phase-03-test-coverage-and-proof-harness | dotnet test + Playwright artifacts | Targeted unit/integration tests exist, fake IPFS/FTP harness needs are addressed honestly, and command list is reproducible. |
| RQ-014 | 03-phase-03-test-coverage-and-proof-harness | dotnet test + Playwright artifacts | Targeted Playwright tests exist and screenshot artifacts are produced for the changed surfaces. |
| RQ-015 | 03-phase-03-test-coverage-and-proof-harness | dotnet test + Playwright artifacts | Execution bundle and QA prompt both require manual MCP proof and screenshot review before closure. |
| RQ-016 | 04-phase-04-cross-project-adoption-ui-and-validation | Playwright MCP screenshots + execution report + QA audit | Main checklist, execution report, QA audit, and raw-note closure map are all complete and consistent. |

## Checklist usage

- Every requirement above must appear in at least one phase README acceptance checklist.
- `reviews/01-execution-report.md` must record which commands and screenshots closed the requirement during implementation.
- `reviews/02-qa-coverage-audit.md` must compare this matrix against the XLSX touchpoint inventory before final closure.
