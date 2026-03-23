# 03 - Repair Specification

This document defines what Codex must implement to repair the gap between the intended product and the current implementation.

## 1. Shell and navigation recovery

### Desired outcome

The shell must behave like a daily delivery workstation.

### Mandatory changes

- Upgrade the left rail from a flat route list to a real workspace menu.
- Add workspace and project context switching.
- Show opened projects and opened prompt sessions as workbench items, not only global routes.
- Standardize the top bar, right rail, and action ordering across modules.
- Keep cards as the default list pattern.

### Hard rules

- Do not remove the existing internal tab baseline.
- Extend it from route-first to artifact-first.
- Avoid adding browser-tab assumptions back into the UX.

## 2. Internal tab model recovery

### Desired outcome

Tabs represent real work items and can be safely restored.

### Mandatory changes

- Introduce explicit tab kinds for:
  - project overview
  - project structure
  - project calendar
  - prompt wizard session
  - validation run
  - test plan
  - prompt detail
  - settings
- Restore tabs by artifact identity plus snapshot, not only by route.
- Add richer actions:
  - close others
  - close to the right
  - close all background
  - reopen recent
  - overflow search
- Persist meaningful snapshots for wizard sessions and workbench surfaces.

### Hard rules

- Sleeping tabs must release heavy state.
- Dirty state must be visible before opening a tab.
- Rehydration failures must degrade safely.

## 3. Unified project object graph

### Desired outcome

The project structure canvas becomes a real authoring graph.

### Mandatory changes

- Add a shared project-object contract, for example:
  - `IProjectObject`
  - `ProjectObjectBase`
  - `ProjectObjectType`
  - `ProjectObjectLink`
  - `ProjectObjectVisualProfile`
- Model project-linked items through typed object classes or descriptors.
- Keep resource-specific configuration, but expose project objects through one shared graph contract.
- Support graph relationships such as depends-on, uses, validates, blocks, derived-from, and belongs-to.

### Required object families

- phase and milestone objects
- repository and file objects
- link and connector objects
- prompt flow and prompt session objects
- validation and test objects
- notes and decision objects
- secret reference objects

### Hard rules

- The graph is not only a read projection.
- Users must be able to create and connect objects directly from the workbench.

## 4. Real canvas and calendar integration

### Desired outcome

The workbench uses the documented engines, not placeholder HTML lists.

### Mandatory changes

- Replace `src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js` placeholder behavior with a real wrapper around the documented source-pack engines.
- Preserve the C#-owns-logic rule.
- Implement:
  - persistent node positions
  - viewport restore
  - selection restore
  - node creation and linking
  - right-click grouped hex menu
  - artifact opening from nodes and events
- Add typed visual profiles per node type:
  - shape
  - color
  - icon
  - accent badges

### Hard rules

- JavaScript owns rendering, input capture, and viewport mechanics only.
- Commands, validation, persistence, and graph rules stay in C#.

## 5. Project wizard recovery

### Desired outcome

Project creation and editing feel guided and comfortable.

### Mandatory changes

- Replace the current direct editor as the primary path with a wizard-first flow.
- Allow the wizard to open either:
  - in a modal for simple creation
  - in a dedicated internal tab for longer sessions
- Break the flow into steps:
  - identity
  - dates and phases
  - stack profile
  - linked objects
  - review and next actions
- Make the project canvas a first-class follow-up workspace immediately after creation.

### Hard rules

- Do not lose existing persistence logic.
- Reuse existing services where possible, but do not expose raw CRUD as the main UX.

## 6. Prompt wizard and prompt-flow recovery

### Desired outcome

Prompt generation becomes a visual, session-based workflow rather than a flat form.

### Mandatory changes

- Open prompt wizard sessions as dedicated internal tabs.
- Represent prompt sessions and prompt nodes in the workbench graph.
- Support branching from any prompt step.
- Add a clear stage-based wizard UI with session persistence.
- Add governance UI for:
  - shared prompt blocks
  - prompt flow templates
  - auto-apply rules by prompt type, blueprint, or phase

### Hard rules

- Shared blocks must remain centrally governed.
- Prompt wizard code must not fall back to page-local hardcoded instruction strings.

## 7. Resource editor recovery

### Desired outcome

Resource management feels typed and comfortable.

### Mandatory changes

- Replace the generic-only editor with resource-type-specific editors.
- Keep a shared base flow, but render type-specific forms for:
  - repository
  - folder
  - file
  - web link
  - FTP
  - SSH
  - PowerShell script
  - Docker or Docker Compose
  - secret link
  - prompt link
- Map resource types into the unified project object graph with their own visual profile.

### Hard rules

- `ConfigJson` may remain as an internal storage mechanism, but it must not remain the primary user-facing editing experience.

## 8. Manager and tuning recovery

### Desired outcome

The tuning loop becomes real and trustworthy.

### Mandatory changes

- Replace simulated Codex lifecycle progression with a real local adapter or Codex CLI execution path.
- Support screenshot or clipboard image attachment in tuning requests.
- Attach capsule, route, project, tab, and selection context.
- Keep the current watch-ready gate and capsule-drift gate.
- Persist enough evidence to troubleshoot failed tuning cycles.

### Hard rules

- Mutating operations remain local-only and token-protected.
- No unsafe secrets or attachments are logged casually.
- "Codex finished" and "watch ready" stay distinct states.

## 9. Testing and QA recovery

### Desired outcome

The repaired product has credible regression protection.

### Mandatory changes

- Fix the Playwright fixture cleanup failure.
- Add real UI and integration coverage for:
  - wizard-first project creation
  - prompt wizard session tabs
  - real canvas interactions
  - real calendar interactions
  - node creation and branching
  - typed resource editors
  - tuning request submission with fake or controlled adapter

### Hard rules

- Do not accept placeholder wrappers as "covered" if the tests only exercise placeholder list rendering.
