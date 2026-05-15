# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `N001`, `R001` | `inputs/02-structured-input.md`, `inventories/01-scope-inventory.md`, `inventories/page-refactor-checklist.xlsx` | preparation, `09` | workbook inspection and prepared validator | Every route is inventoried; not every tiny page must be edited. |
| `N002`, `R007` | `requirements/01-normalized-requirements.md`, `subbundles/02-project-structure-page-shell-components` | `02` | `ProjectStructurePageTests`, browser route proof, screenshots | Depends on `01`. |
| `N002`, `R008` | `requirements/01-normalized-requirements.md`, `subbundles/04-prompt-factory-page-shell-components` | `04` | `PromptFactoryPageTests`, PromptFactory Playwright proof | Depends on `03`. |
| `N002`, `R009` | `requirements/01-normalized-requirements.md`, `subbundles/08-process-and-workflow-editor-page-decomposition` | `08` | workflow/process component tests and browser proof | Uses component-only decomposition where logic is already delegated. |
| `N003`, `R002` | `subbundles/01-project-structure-node-helpers` | `01` | helper/unit tests plus ProjectStructure component tests | Critical foundation. |
| `N003`, `R003` | `subbundles/03-prompt-factory-canvas-helpers` | `03` | canvas adapter tests and PromptFactory page tests | Critical foundation. |
| `N003`, `R004` | `subbundles/05-plugin-page-helpers-and-render-fragments` | `05` | `PluginsPageTests` | Critical for plugin page test ids. |
| `N003`, `R005` | `subbundles/06-crm-hr-page-helper-extraction` | `06` | CRM/HR component and Playwright tests | Sensitive-data proof required. |
| `N003`, `R006` | `subbundles/07-workspace-settings-helper-extraction` | `07` | `SettingsPageDataSourcesTests` plus build | Settings UI proof required after component edits. |
| `N005` | `inventories/page-refactor-checklist.xlsx` | preparation | workbook render and export verification | Final workbook must be linked in closure. |
| `N006` | `plan/01-phase-plan.md` | all subbundles | entry and closure gate rows | Helper phases precede component phases. |
| `N007`, `R011` | `reviews/01-execution-report.md`, `subbundles/10-final-regression-proof-and-closure` | `10` | build, tests, Playwright screenshots, completed validator | Raw note closure cannot remain pending. |
