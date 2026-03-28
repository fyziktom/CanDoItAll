# Current State

## Relevant Code Surfaces

- Projects domain and service:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs`
- Projects page:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- Workbench persistence and sync:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchSchemaInitializer.cs`
- Canvas projection and actions:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureGraphAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.NodeQuickActions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Workflows.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- Existing automated coverage:
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectsPageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureGraphAdapterTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectsServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`
- Repo skill pack and sync scripts:
- `C:\repositories\CanDoItAll\codex\skills\bundles\candoitall-bundle-workflow\SKILL.md`
- `C:\repositories\CanDoItAll\codex\skills\bundles\candoitall-bundle-preparation\SKILL.md`
- `C:\repositories\CanDoItAll\codex\skills\bundles\candoitall-bundle-execution\SKILL.md`
- `C:\repositories\CanDoItAll\codex\scripts\install-candoitall-skills.ps1`
- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1`

## Projects Domain Baseline

- `Project` currently stores only its own fields, phases, and option selections.
- `ProjectsService.ListAsync` returns `ProjectSummary(Id, Name, Status, CurrentPhase, PhaseCount, UpdatedAtUtc)` with no hierarchy counts, no parent/child references, and no traversal helpers.
- `ProjectsService.GetAsync` and `SaveAsync` know nothing about parent projects, child projects, or relation editing.
- `ProjectsServiceIntegrationTests` only prove search/activity behavior for ordinary project save.

## Projects Page Baseline

- `/projects` is a card-first workspace with search and status filters only.
- Each card exposes `View`, `Dashboard`, `Structure`, and `Calendar` actions.
- The page has no hierarchy filter state, no parent lookup, no child lookup, and no relation affordance on the card or modal.
- The existing modal split is overview/editor. There is no recursive related-project navigation surface yet.
- `ProjectsPageTests` are minimal and do not exercise hierarchy UI.

## Workbench Persistence Baseline

- `ProjectWorkbenchService.SyncGraphAsync` creates one system-managed `project:{id}` root node plus other system-managed nodes for phases, resources, prompt runs, validations, and test plans.
- `ProjectObjectRecord` has a single `ParentNodeKey`, which makes node hierarchy strictly single-parent.
- `CreateObjectAsync` writes `ParentNodeKey` and also creates a `BelongsTo` link when a parent is supplied.
- `ReparentObjectAsync` removes the old parent-style link and replaces it with one new parent, again assuming singular parentage.
- `ProjectObjectLinkRecord` already allows multiple arbitrary links, but the current project hierarchy does not use it as a first-class project-to-project relation model.

## Structure Canvas Baseline

- `ProjectStructureGraphAdapter` maps each node to one `ParentId` and separately maps explicit links.
- `ProjectStructureActionCatalogAdapter` already has generic `connect`, `reconnect`, and `disconnect` actions, plus existing quick actions that can open some nodes in a new tab.
- `ProjectStructurePage` already supports selection windows, quick action dialogs, new-tab opening for some node types, and reconnect flows for existing workbench nodes.
- The canvas currently has no notion of "related project" nodes beyond the one current project root node.
- There is no current styling or metadata contract for a subdued or disabled parent-project node that exists only to explain a multi-parent child.

## Test And Proof Baseline

- Integration tests already cover workbench schema repair, basic structure projection, create/update/delete/reparent flows, and some typed metadata behavior.
- Component tests already cover substantial project-structure page behavior and quick-action modals.
- There is no test coverage for project-to-project hierarchy relations, cycle prevention, Projects page hierarchy UX, or hierarchy-specific canvas projection.
- Browser validation for this feature does not exist yet.

## Skill-Pack Baseline

- The repo-local custom skill pack contains `candoitall-bundle-workflow`, `candoitall-bundle-preparation`, and `candoitall-bundle-execution`, but it does not currently contain repo-local copies of `candoitall-bundle-validator` or `candoitall-subbundle-validator`.
- The repo-local `codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py` validates structure only and does not yet support the staged `--stage prepared` / `--stage completed` flow described by the newer workflow instructions used in this run.
- `codex/scripts/install-candoitall-skills.ps1` only installs five custom skills and therefore would not propagate validator-skill changes even if they were added later.
- `tools/Reinstall-CanDoItAllMcps.ps1` does recursively sync repo-managed skills, so once the repo skill pack is complete it can distribute them, but the install script still lags behind.
