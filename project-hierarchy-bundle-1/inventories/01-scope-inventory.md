# Scope Inventory

## Application Code Inventory

| Area | Current files | Why they matter |
| --- | --- | --- |
| Projects domain | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs` | Owns project persistence, summaries, and the smallest coherent place to add hierarchy relations. |
| Projects UI | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor` | Owns card layout, filters, modal flows, and existing related project navigation actions. |
| Workbench persistence | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs` | Owns structure-surface sync, node creation, reparent behavior, and projected links. |
| Workbench SQLite repair | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchSchemaInitializer.cs` | May need schema compatibility updates if hierarchy projection changes touch stored workbench data. |
| Canvas projection | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureGraphAdapter.cs` | Decides parent/child rendering, palette/styling hooks, and related-project node mapping. |
| Canvas actions | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs` | Decides whether hierarchy-specific add/open/reconnect actions are first-class. |
| Canvas page behavior | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.*` | Owns quick actions, JS `open` usage, reconnect UX, and visible structure-canvas workflows. |

## Test Inventory

| Test surface | Current files | Gap to close |
| --- | --- | --- |
| Projects page component tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectsPageTests.cs` | No hierarchy filters, modal recursion, or related-project assertions. |
| Structure canvas component tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs` | No project-to-project hierarchy node coverage yet. |
| Graph adapter tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureGraphAdapterTests.cs` | No subdued extra-parent node styling or hierarchy projection coverage yet. |
| Integration tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectsServiceIntegrationTests.cs`, `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs` | No many-parent persistence or cycle-guard coverage yet. |

## Skill-Pack Inventory

| Area | Current files | Gap to close |
| --- | --- | --- |
| Repo workflow skills | `C:\repositories\CanDoItAll\codex\skills\bundles\candoitall-bundle-workflow\SKILL.md`, `C:\repositories\CanDoItAll\codex\skills\bundles\candoitall-bundle-preparation\SKILL.md`, `C:\repositories\CanDoItAll\codex\skills\bundles\candoitall-bundle-execution\SKILL.md` | Need analytics-driven improvement review after feature execution. |
| Missing repo validator skills | No repo-local `candoitall-bundle-validator` or `candoitall-subbundle-validator` directories currently exist. | Must add repo-managed copies if the repaired workflow depends on them. |
| Skill install script | `C:\repositories\CanDoItAll\codex\scripts\install-candoitall-skills.ps1` | Currently installs only five custom skills and would miss validator-skill changes. |
| Reinstall/sync script | `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1` | Already syncs repo-managed skills recursively, but must stay aligned with the repo skill-pack layout. |
