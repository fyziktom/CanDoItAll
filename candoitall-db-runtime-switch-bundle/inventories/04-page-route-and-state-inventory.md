# Page, Route, And State Inventory

## Routed Pages

| Route | Source | Switch-Relevant Risk |
| --- | --- | --- |
| `/` and `/dashboard` | `src/CanDoItAll.Web/Components/Pages/Home.razor` | Startup modal and active-profile status should surface here |
| `/settings` | `src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor` | Data Sources tab must land here |
| `/activity` | `src/CanDoItAll.Modules.Activity/Pages/ActivityPage.razor` | Must reflect active DB after switch |
| `/automation` | `src/CanDoItAll.Modules.Automation/Pages/AutomationPage.razor` | Must reflect active DB after switch |
| `/projects` | `src/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor` | Project list is a primary isolation target |
| `/prompt-gallery` | `src/CanDoItAll.Modules.Prompts/Pages/PromptGalleryPage.razor` | Must reflect active DB after switch |
| `/resources` | `src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor` | Must reflect active DB after switch |
| `/validation` | `src/CanDoItAll.Modules.Validation/Pages/ValidationCenterPage.razor` | Must reflect active DB after switch |
| `/test-lab` | `src/CanDoItAll.Modules.TestLab/Pages/TestLabPage.razor` | Must reflect active DB after switch |
| `/prompt-factory` | `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor` | Uses DB + managed media |
| `/projects/{projectId}/structure` | `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor` | Currently prone to stale-project failure after switch |
| `/projects/{projectId}/calendar` | `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor` | Currently prone to stale-project failure after switch |

## Browser/Workbench State Surfaces

| Surface | Source | Current Problem |
| --- | --- | --- |
| Main shell/top bar | `src/CanDoItAll.Web/Components/Layout/MainLayout.razor` | No active DB badge, no switcher, no startup modal |
| Browser workbench state store | `src/CanDoItAll.Web/Infrastructure/BrowserWorkspaceStateStore.cs` | One hardcoded local-storage key shared across all DBs |
| Workbench session snapshot | `src/CanDoItAll.SharedKernel/WorkbenchTabState.cs` | No profile/fingerprint marker |
| Workbench state service | `src/CanDoItAll.Modules.Workbench/WorkbenchTabState.cs` | Restores stale tabs across DBs |
| Browser JS bridge | `src/CanDoItAll.Web/wwwroot/js/browserState.js` | No cross-tab switch broadcast support |

## Mandatory Browser Proof Targets

- Startup on last active DB with modal.
- Active DB indicator and quick switcher in the shell.
- Settings Data Sources tab with both SQLite and PostgreSQL forms.
- Artifact route loaded before a switch, then safe fallback after switching to a DB where the artifact does not exist.
- Two pages/tabs open concurrently and both reacting to the same app-wide switch.
