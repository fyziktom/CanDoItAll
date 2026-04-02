
# Normalized Requirements

The raw request was normalized into concrete, testable requirements without silently narrowing the user's absolute language.

| Requirement | Normalized requirement | Owning phase | Completion signal |
| --- | --- | --- | --- |
| RQ-001 | Introduce an extensible storage domain model and provider abstraction that supports FileSystem, IPFS, FTP, and future providers without reworking module call sites. | 01-phase-01-models-interfaces-and-persistence-contracts | Domain models + provider interface + capability model exist and drivers can register by kind. |
| RQ-002 | Persist storage catalog records, secrets linkage, routing defaults, health metadata, and node references in app data with migrations for both SQLite and PostgreSQL. | 01-phase-01-models-interfaces-and-persistence-contracts | New entities and migrations exist in both migration projects and bootstrap/seed upgrade path is documented. |
| RQ-003 | Replace direct relative-path assumptions with storage object references and capability-based access metadata while preserving safe compatibility for existing filesystem-backed flows. | 01-phase-01-models-interfaces-and-persistence-contracts | Compatibility adapter exists and legacy callers can be migrated incrementally. |
| RQ-004 | Implement storage routing and recommendation rules that can choose defaults by usage purpose, file subtype, MIME type, edit intent, and project/node scope. | 01-phase-01-models-interfaces-and-persistence-contracts | Routing contracts, recommendation context, and default policy matrix are implemented and testable. |
| RQ-005 | Implement provider registry, factory, and runtime services that create, test, and invoke FileSystem/IPFS/FTP drivers safely. | 02-phase-02-provider-services-routing-and-batch-pipeline | Registry resolves concrete drivers, connection test path exists, and runtime services are DI-registered. |
| RQ-006 | Implement filesystem, IPFS, and FTP provider drivers plus future-provider extension seams without baking provider-specific branching into modules. | 02-phase-02-provider-services-routing-and-batch-pipeline | Provider folders/classes exist, each driver meets the shared contract, and no module depends on provider-specific classes directly. |
| RQ-007 | Implement a bounded batch transfer/upload pipeline for large folder/file migrations, including progress, cancellation, checksum/verification hooks, and provider capability checks. | 02-phase-02-provider-services-routing-and-batch-pipeline | Transfer manifest + progress + bounded concurrency pipeline exist and at least one folder migration path is covered by tests. |
| RQ-008 | Implement unified access/serving logic for preview/download/open actions so UI can work with local and remote providers safely. | 02-phase-02-provider-services-routing-and-batch-pipeline | Unified access route/service exists, local-open is capability-gated, and preview/download actions no longer assume /managed-files paths only. |
| RQ-009 | Provide reusable UI components for storage summaries, selector/dropdowns, health/capabilities badges, recommendation banners, and wizard steps. | 04-phase-04-cross-project-adoption-ui-and-validation | Reusable components exist in a shared component location and are used by multiple pages. |
| RQ-010 | Provide a settings UI tab and wizard for storage catalog management, connection testing, defaults, and health visibility. | 04-phase-04-cross-project-adoption-ui-and-validation | Settings page exposes storage tab, wizard, test connection flow, and management list/detail interactions. |
| RQ-011 | Support project-structure storage nodes and storage references so projects can attach one or more storage systems and use them for subtree defaults or explicit destinations. | 04-phase-04-cross-project-adoption-ui-and-validation | Users can create a storage node, link it to a storage record, and see/update the reference from project structure. |
| RQ-012 | Adopt the new storage system across all identified upload, preview, export, download, open-local, and snapshot surfaces that are in scope. | 04-phase-04-cross-project-adoption-ui-and-validation | All in-scope touchpoints from the XLSX inventory have implementation ownership, code changes, and proof. |
| RQ-013 | Extend unit, integration, and fake-server coverage for storage routing, provider contracts, migrations, access endpoints, and batch transfer behavior. | 03-phase-03-test-coverage-and-proof-harness | Targeted unit/integration tests exist, fake IPFS/FTP harness needs are addressed honestly, and command list is reproducible. |
| RQ-014 | Extend Playwright automation coverage for storage settings, upload recommendations, previews/downloads, and project-structure storage nodes. | 03-phase-03-test-coverage-and-proof-harness | Targeted Playwright tests exist and screenshot artifacts are produced for the changed surfaces. |
| RQ-015 | Require real Playwright MCP validation with screenshots, large-screen and narrower-width passes, and explicit visual-review questions for every changed UI surface. | 03-phase-03-test-coverage-and-proof-harness | Execution bundle and QA prompt both require manual MCP proof and screenshot review before closure. |
| RQ-016 | Provide execution-grade prompts, checklists, traceability, and QA audits so Codex cannot claim completion without evidence or silently skip blocked proof. | 04-phase-04-cross-project-adoption-ui-and-validation | Main checklist, execution report, QA audit, and raw-note closure map are all complete and consistent. |

## Non-negotiable interpretation rules

- “All uploads and views/downloads/use of files” means the inventory must enumerate persisted and adjacent file surfaces explicitly. Silent omission is not allowed.
- “Codex must be forced to do real validation with Playwright MCP and screenshots” means automated Playwright coverage is necessary but insufficient on its own.
- “Future providers” means the abstraction must be open-ended and provider-specific branching cannot leak into module code.
- “Each storage must have also own node” means project-structure storage-node support is part of scope, not a nice-to-have.
- “Persist settings/configuration of what storage will be used for what in default” means routing policy persistence is first-class and not just a hard-coded enum map.

## Requirement grouping

- Foundation contracts and persistence: `RQ-001` to `RQ-004`
- Runtime services and providers: `RQ-005` to `RQ-008`
- UI and cross-project adoption: `RQ-009` to `RQ-012`
- Proof and execution rigor: `RQ-013` to `RQ-016`
