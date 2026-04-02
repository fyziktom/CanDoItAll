
# Raw Note Closure Matrix

| Raw note | Exact wording | Normalized requirement ids | Impacted surface | Planned proof / bundle location | Owning phase | Exception status |
| --- | --- | --- | --- | --- | --- | --- |
| N001 | We have simple driver in WorkspaceStorage.cs. It solves just small part of what we need. | RQ-001, RQ-003 | Infrastructure storage baseline | `analysis/01-current-state.md`, Phase 01 README | Phase 01 | None |
| N002 | Build a flexible, robust, and safe storage driver that supports file system, IPFS (local or remote node), FTP (local or remote), and future storage types. | RQ-001, RQ-005, RQ-006 | Provider architecture and runtime services | Phase 01 + Phase 02 READMEs and architecture | Phase 01/02 | None |
| N003 | Persist settings/configuration of what storage will be used for what by default. | RQ-002, RQ-004, RQ-010 | Storage catalog, routing defaults, settings UI | Persistence model + settings UI plans | Phase 01/04 | None |
| N004 | When uploading files, recommend a default storage choice based on file type and editability expectations. | RQ-004, RQ-012 | Upload flows and recommendation policy | `requirements/03-default-routing-policy.md` + Phase 04 workstreams | Phase 01/04 | None |
| N005 | Add a UI section for storage settings, including adding a new storage through a wizard with type choice, connection info, and connection test. | RQ-009, RQ-010 | Settings UI and wizard | Phase 04 settings UI workstream | Phase 04 | None |
| N006 | Provide generic reusable UI components for storage management so multiple pages can share the same codebase. | RQ-009 | Reusable UI components | Phase 04 shared-components workstream | Phase 04 | None |
| N007 | Split work into phases with own folders and subbundles: models/interfaces; factories/services/implementations; unit tests; implementation into other projects. | RQ-016 | Bundle structure and execution sequencing | Phase folders + plan docs | Bundle-wide | None |
| N008 | Prepare for batch loading/uploads and migration of folders/content to IPFS, FTP, and future providers. | RQ-007, RQ-012 | Transfer pipeline and snapshot/migration flows | Phase 02 transfer pipeline workstream | Phase 02/04 | None |
| N009 | Allow references from project structure nodes to storage systems. Each storage also needs its own node, and creating a node may also create the storage connection. | RQ-011, RQ-012 | Project structure nodes and storage references | Phase 04 storage-node workstream | Phase 01/04 | None |
| N010 | Map all uploads, views, downloads, and file-use situations across the app. Best deliver that mapping as XLSX and derive subbundles from it. | RQ-012, RQ-016 | Cross-module touchpoint inventory | Workbook + touchpoint traceability | Bundle-wide / Phase 04 | None |
| N011 | Give Codex explicit checklists so it cannot skip work. | RQ-016 | Main checklist and prompts | Main checklist + prompts | Bundle-wide | None |
| N012 | Force real validation with Playwright MCP and screenshots. | RQ-014, RQ-015, RQ-016 | Playwright MCP proof contract | Phase 03 README + QA prompt + execution report | Phase 03/04 | None |
| N013 | Perform a senior-QA-style validation against the XLSX to confirm every identified refactor item has a proper subbundle and appears in the main checklist. | RQ-016 | QA coverage audit against XLSX | QA coverage audit review file | Phase 04 | None |
| N014 | Final zip must contain detailed instructions, prompts, tests, validation criteria, and UI quality checks including overlays, overflows, text/image clipping, and other best practices. | RQ-014, RQ-015, RQ-016 | Final zip contents and visual quality criteria | Bundle contents + screenshot criteria | Phase 03/04 | None |

## Interpretation guardrails

- No raw note above is silently discarded.
- Adjacent surfaces stay visible through the touchpoint inventory even when Phase 04 decides they are intentionally unchanged.
