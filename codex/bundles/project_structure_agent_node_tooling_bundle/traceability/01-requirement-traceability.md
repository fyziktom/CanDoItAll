# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| N001 / R001 page title | `requirements/01-normalized-requirements.md` | `subbundles/01-project-structure-page-title` | Component test and optional browser title check | Independent. |
| N002 / R002 / R003 work task and catalog | `analysis/01-current-state.md`, `architecture/01-target-solution.md` | `subbundles/02-agent-node-catalog-and-context` | Integration tests for catalog and MAF tool list | Critical foundation. |
| N005 / R004 selected context | `architecture/01-target-solution.md` | `subbundles/02-agent-node-catalog-and-context` | Component/unit test for contextual prompt and metadata | Prerequisite for subbundle 03. |
| N004-N006 / R005-R006 selected nodes to subproject | `architecture/01-target-solution.md` | `subbundles/03-selected-node-subproject-tooling` | Integration/API test for new workflow | Critical foundation. |
| N007 / R007 dependencies | `requirements/01-normalized-requirements.md` | `subbundles/03-selected-node-subproject-tooling` | Integration/API dependency readback | Preserve internal links. |
| N008-N009 / R008 scenario workbook | `requirements/01-normalized-requirements.md`, `outputs/project-structure-agent-generic-scenarios.md` | `subbundles/04-generic-agent-scenarios-workbook` | Markdown fallback plus execution report blocker | XLSX blocked by unavailable `@oai/artifact-tool`; scenario content includes architect examples and additional user stories. |
