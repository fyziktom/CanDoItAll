# 02 - Audit Findings

This audit compares the current implementation with the consolidated requirements.

## 1. Working as expected

### 1.1 Architectural foundation is strong

The solution structure under `src/`, `tools/`, and `tests/` already reflects a modular monolith with separated modules for projects, resources, prompts, factory, validation, test lab, workbench, workspace, security, activity, automation, web shell, and the manager sidecar.

### 1.2 Persistence baseline is real

The infrastructure already includes:

- EF Core setup
- SQLite and PostgreSQL support
- `AppDbContext`
- `IDbContextFactory`
- runtime database creation

Relevant evidence:

- `src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `src/CanDoItAll.Infrastructure/Persistence/AppDbContextFactory.cs`
- `src/CanDoItAll.Web/Program.cs`

### 1.3 Secret and provider baselines exist

Secret encryption, provider profiles, and provider health checks are implemented as real services, not page-local placeholders.

Relevant evidence:

- `src/CanDoItAll.Modules.Security/SecurityModels.cs`
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs`

### 1.4 Prompt library, validation, test lab, and activity are real modules

The application already stores and displays:

- prompt drafts and versions
- validation runs and findings
- test plans and evidence
- activity history and search
- background job state

Relevant evidence:

- `src/CanDoItAll.Modules.Prompts/Pages/PromptGalleryPage.razor`
- `src/CanDoItAll.Modules.Validation/Pages/ValidationCenterPage.razor`
- `src/CanDoItAll.Modules.TestLab/Pages/TestLabPage.razor`
- `src/CanDoItAll.Modules.Activity/Pages/ActivityPage.razor`
- `src/CanDoItAll.Modules.Automation/Pages/AutomationPage.razor`

### 1.5 Manager, watch, and capsule foundation exists

The repository already contains a real `CanDoItAll.Manager` project with:

- watch supervision
- ready-state waiting
- log and event APIs
- capsule refresh and coverage
- tuning request endpoints

Relevant evidence:

- `tools/CanDoItAll.Manager/Program.cs`
- `tools/CanDoItAll.Manager/WatchSupervisorService.cs`
- `tools/CanDoItAll.Manager/CapsuleCatalogService.cs`

### 1.6 Automated test baseline exists

Unit, integration, component, and Playwright test projects are present and active.

Verification result:

- unit tests passed
- integration tests passed
- component tests passed
- Playwright test bodies passed

Residual issue:

- the Playwright project currently returns a failing exit because fixture cleanup tries to delete SQLite shared-memory files while still locked
- evidence: `tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs`

## 2. Working, but below expectation

### 2.1 Left rail exists, but not at the requested product level

The shell already has a left rail, but it is a flat route list. It does not yet behave like the stronger enterprise-style workspace navigation the requirements call for.

Current limitation:

- no workspace switcher
- no project-scoped subnavigation
- no "opened projects" or "opened sessions" grouping
- no strong workbench context hierarchy

Relevant evidence:

- `src/CanDoItAll.ComponentKit/Components/AppShell.razor`
- `src/CanDoItAll.Web/Composition/ShellNavigation.cs`

### 2.2 Internal tabs are real, but route-centric

The workbench service already supports restore, sleep, pin, reorder, and dirty-state flags, which is good. The problem is that the tab model still tracks routes first, not concrete artifact sessions first.

Current limitation:

- opened project tabs are not first-class
- prompt wizard sessions are not distinct internal work items
- validation/test artifacts do not drive explicit artifact-tab identity
- restore snapshots are shallow because the product is mostly reopening routes

Relevant evidence:

- `src/CanDoItAll.Modules.Workbench/WorkbenchTabState.cs`
- `src/CanDoItAll.Web/Components/Layout/MainLayout.razor`

### 2.3 Card-based lists are partially present

Several screens already render list entries as cards rather than tables, which is aligned with the UX direction.

Current limitation:

- the list surfaces are still attached to raw editor forms
- there is little guided flow or progressive disclosure
- the visual system is not yet shaped into a strong enterprise workbench

Relevant evidence:

- `src/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor`
- `src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor`
- `src/CanDoItAll.Modules.Prompts/Pages/PromptGalleryPage.razor`

### 2.4 Shared prompt blocks and prompt runs exist, but only as a thin baseline

The factory service already has:

- `PromptBlockDefinition`
- `PromptFlowTemplate`
- `PromptRun`
- `PromptRunNode`

Current limitation:

- only seeded defaults exist
- there is no governance UI for maintaining the catalog
- auto-application rules are still simplistic
- the visual workbench experience for these nodes is missing

Relevant evidence:

- `src/CanDoItAll.Modules.Factory/FactoryDomain.cs`
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs`

## 3. Missing or incorrectly implemented

### 3.1 Critical: the structure canvas and calendar wrappers are placeholders

The current JS interop file does not wrap the intended engines. It renders button-card lists in plain DOM.

Impact:

- no real canvas behavior
- no real mind-map or graph editing
- no real calendar layout
- no viewport persistence
- no drag and drop graph editing
- no real right-click hex menu

Relevant evidence:

- `src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js`

This is the clearest mismatch between the documented architecture and the actual implementation.

### 3.2 Critical: project authoring is CRUD-first instead of wizard-first and canvas-first

The current Projects page is a direct edit form with inline phase rows and option fields.

Impact:

- no guided project creation wizard
- no dedicated project authoring tab or modal flow
- no comfortable progressive disclosure
- no canvas-driven authoring of project structure

Relevant evidence:

- `src/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor`

### 3.3 Critical: prompt factory is not the intended prompt wizard experience

The page is a single large form with selectors and a generated prompt box. It is functional, but it is not the step-driven prompt wizard and prompt-sequence workspace requested in the source inputs.

Impact:

- prompt work is not visually staged
- prompt sessions are not first-class workbench tabs
- parallel branch handling is not visible in the UI
- users still operate a form rather than a guided flow workspace

Relevant evidence:

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`

### 3.4 Critical: the unified project object graph is missing

The current implementation has `Project`, `ProjectPhase`, `ProjectResource`, `PromptRun`, `ValidationRun`, and `TestPlan`, but it does not yet have the intended shared project-object contract that unifies these into one editable graph model.

Impact:

- the structure view is a read-model aggregation, not a true authoring graph
- node-type specific behavior is not modeled centrally
- there are no visual profiles by object kind
- there is no comfortable way to add arbitrary project-linked objects directly from the canvas

Relevant evidence:

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs`

### 3.5 Major: the right-click hex menu is not actually implemented

The structure page currently shows visible hex-shaped buttons inside the inspector. That is not the requested grouped right-click menu integrated with the canvas.

Relevant evidence:

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`

### 3.6 Major: workbench artifact opening still routes through page navigation

The code navigates to routes, then relies on route tracking to populate tabs. That is a good baseline, but it falls short of the requested artifact/session-centered workbench model.

Relevant evidence:

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor`
- `src/CanDoItAll.Modules.Workbench/WorkbenchTabState.cs`

### 3.7 Major: resource editing is generic, not type-specific

The current Resources page uses one generic editor with `ConfigJson`. That is not the comfortable typed experience requested for FTP, SSH, repositories, links, scripts, secrets, and prompts.

Relevant evidence:

- `src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor`
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs`

### 3.8 Major: tuning mode is simulated, not connected to real Codex execution

The manager currently simulates Codex lifecycle progression instead of orchestrating a real local adapter or Codex CLI run.

Relevant evidence:

- `tools/CanDoItAll.Manager/TuningRequestService.cs`

### 3.9 Major: screenshot and clipboard input are not wired through the tuning flow

The source requirements explicitly call for screenshot or clipboard image support. The current boundary and client only submit capsule, route, project, tab, selection, and instruction.

Relevant evidence:

- `src/CanDoItAll.ComponentKit/Components/TunableComponentBoundary.razor`
- `src/CanDoItAll.Web/Infrastructure/DevelopmentManagerClient.cs`
- `tools/CanDoItAll.Manager/TuningRequestService.cs`

### 3.10 Major: shell context is too shallow for daily work

The shell shows current route and active tab, but it does not yet show the richer daily-work context requested by the source inputs.

Missing examples:

- active project header with quick switch
- phase-aware left navigation
- open project/session groupings
- more deliberate right-rail action patterns

Relevant evidence:

- `src/CanDoItAll.Web/Components/Layout/MainLayout.razor`
- `src/CanDoItAll.ComponentKit/Components/AppShell.razor`

## 4. Conclusion

The codebase is not a failed prototype. It is a good foundation that stopped too early at the "functional page and service" stage.

The required recovery is not "rewrite everything." The required recovery is:

- replace placeholder visual adapters with the real engines
- move from route-level pages to artifact/session workbench semantics
- introduce the unified project object graph
- move major flows to wizard-first UX
- complete the real manager-to-Codex tuning loop
