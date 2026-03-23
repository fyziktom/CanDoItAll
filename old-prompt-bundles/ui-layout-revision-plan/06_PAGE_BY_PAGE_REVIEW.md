# Page By Page Review

## Review Legend

- `Priority`: implementation value in phase 1
- `Risk`: likelihood of unintended breakage
- `Phase`: recommended order in the migration sequence

---

## Dashboard

- Route: `/`
- Component: `src/CanDoItAll.Web/Components/Pages/Home.razor`
- Current role: informational launch page
- Likely user story: "I just opened the app. Show me where to resume work or what to do first."
- Current problems:
  - no primary call to action
  - repeated shell context plus page intro
  - content is system-explanatory rather than workflow-oriented
  - no recent work, no quick start, no resume path
- Recommended improvements:
  - convert to a real start surface
  - add primary header actions such as `New project` and `Open projects`
  - show recent project tabs, prompt sessions, validations, or background issues
  - demote system/status explanation to secondary cards
- Candidate shared components:
  - `PageScaffold`
  - `PageActionHeader`
  - `QuickActionGrid`
  - `RecentWorkList`
  - `EmptyState`
- Complexity / risk: low
- Priority: medium-high
- Phase: after shell foundations

## Projects

- Route: `/projects`
- Component: `src/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor`
- Current role: project list plus wizard-first editor
- Likely user story: "Create a new project or reopen an existing one and continue planning."
- Current problems:
  - primary actions live in the list card instead of the page header
  - list items do not show selected state
  - no search/filter/sort for project list
  - list item actions are equally weighted even though `Open work item` is the real primary path
  - wizard content is long and sectioned only by ad hoc step buttons
  - save/delete/open actions are not sticky
- Recommended improvements:
  - use a standard list/detail shell
  - move `New project` to the page header
  - add selected-row styling and optional project search
  - make list-row click open/select, move secondary row actions to a compact action region
  - standardize wizard step header and sticky action footer
  - add a lightweight review summary area that stays visible near save actions
- Candidate shared components:
  - `ListDetailShell`
  - `ListPanelHeader`
  - `SelectionListItem`
  - `WorkflowStepper`
  - `FormSection`
  - `StickyActionFooter`
- Complexity / risk: medium
- Priority: high
- Phase: early page migration

## Project Structure

- Route: `/projects/{projectId}/structure`
- Component: `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- Current role: protected project graph workbench
- Likely user story: "Work directly on project structure artifacts and dependencies in a focused canvas environment."
- Current problems:
  - surrounding shell is too tall and noisy for a focus surface
  - duplicate route intro competes with the stage
  - global shell right rail is not helpful here
- Recommended improvements:
  - phase 1: protected route
  - give it a `FocusWorkbench` shell mode only
  - hide or collapse the global right rail
  - keep the page body as wide as practical
  - do not redesign internal workbench composition in phase 1
- Candidate shared components:
  - `FocusWorkbenchShellMode`
- Complexity / risk: very high
- Priority: high, but shell-only
- Phase: after shell foundations, before QA

## Project Calendar

- Route: `/projects/{projectId}/calendar`
- Component: `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor`
- Current role: schedule overview with linked artifact opening
- Likely user story: "Review project timing and open the related artifact from the schedule."
- Current problems:
  - loading state is only plain text
  - event detail panel is thin and not especially informative
  - no legend, no summary metrics, no explicit filters
  - shell chrome still consumes too much attention for a schedule page
- Recommended improvements:
  - add standard loading state
  - strengthen the details panel with a key-value block and event metadata
  - add a small schedule legend/status summary if the surface supports it safely
  - consider the same quieter shell mode as other workbench-adjacent routes if it improves focus
- Candidate shared components:
  - `LoadingState`
  - `SplitPanelPage`
  - `KeyValueBlock`
  - `SummaryTiles`
- Complexity / risk: medium
- Priority: medium
- Phase: after major CRUD pages

## Resources

- Route: `/resources`
- Component: `src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor`
- Current role: typed resource registry and editor
- Likely user story: "Register and maintain the project assets, connections, and secret-backed endpoints that prompt/testing workflows depend on."
- Current problems:
  - list has no selected state or filters
  - resource types create very different form lengths, but the page treats them as one uninterrupted editor
  - the descriptor/help block is useful but not integrated into a stronger form structure
  - action area is weak for a long, dynamic form
  - no standard empty state when resource list is empty
- Recommended improvements:
  - introduce filter/search by project, kind, and validation state
  - use selected-row styling
  - split editor into sections: association, connection details, classification, validation/capabilities, notes
  - add sticky save/reset/delete actions
  - standardize the descriptor/help block as a reusable context hint
- Candidate shared components:
  - `ListDetailShell`
  - `FilterBar`
  - `FormSection`
  - `ContextHint`
  - `StickyActionFooter`
  - `EmptyState`
- Complexity / risk: medium
- Priority: high
- Phase: early page migration

## Prompt Gallery

- Route: `/prompt-gallery`
- Component: `src/CanDoItAll.Modules.Prompts/Pages/PromptGalleryPage.razor`
- Current role: prompt artifact library and version history
- Likely user story: "Find an existing prompt, edit a draft, finalize a version, or inspect usage."
- Current problems:
  - no search/filter by phase, status, collection, or tags
  - selected prompt is not obvious in the list
  - collection creation is a body-level action rather than part of a stronger list header pattern
  - versions and usage are nested as heavy sub-cards inside the editor card
  - the page is mixing editing and history without a clear subsection model
- Recommended improvements:
  - use a standard list/detail shell
  - add a filter bar for status/collection/phase
  - move versions and usage into lighter-weight subsections or a secondary tab set in the detail pane
  - keep `Create final version` prominent but clearly secondary to draft editing until content is ready
- Candidate shared components:
  - `ListDetailShell`
  - `FilterBar`
  - `SecondaryTabs`
  - `HistoryList`
  - `DetailSection`
- Complexity / risk: medium
- Priority: medium-high
- Phase: mid-page migration

## Prompt Factory

- Route: `/prompt-factory`
- Component: `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`
- Current role: protected prompt-session workbench
- Likely user story: "Assemble prompt context, build the flow, inspect steps, preview the generated prompt, and send or save it."
- Current problems:
  - surrounding shell is still too loud for a high-focus work surface
  - page-level step chips plus shell chrome add extra vertical load
  - supporting panels are dense, but changing them is risky in phase 1
- Recommended improvements:
  - phase 1: protected route
  - apply only the quieter `FocusWorkbench` shell mode
  - preserve inspector/supporting panels and internal workbench behavior
  - document a future phase for deeper prompt-workbench IA cleanup if needed
- Candidate shared components:
  - `FocusWorkbenchShellMode`
- Complexity / risk: very high
- Priority: high, but shell-only
- Phase: after shell foundations, before QA

## Validation Center

- Route: `/validation`
- Component: `src/CanDoItAll.Modules.Validation/Pages/ValidationCenterPage.razor`
- Current role: validation run launcher and findings review page
- Likely user story: "Run a rule-based review on an artifact, inspect findings, and record the decision."
- Current problems:
  - list has no selected state or filters
  - source content editor dominates the page before results are visible
  - decision control is buried in the findings section
  - results do not get a strong summary header
  - run/reset actions are not anchored for long content
- Recommended improvements:
  - add filters by validation type and decision
  - split the detail area into input, run summary, and findings sections
  - surface decision and finding counts at the top of the result state
  - add sticky action region for `Run validation`
- Candidate shared components:
  - `ListDetailShell`
  - `FilterBar`
  - `ResultSummary`
  - `FindingsList`
  - `StickyActionFooter`
- Complexity / risk: medium
- Priority: high
- Phase: early page migration

## Test Lab

- Route: `/test-lab`
- Component: `src/CanDoItAll.Modules.TestLab/Pages/TestLabPage.razor`
- Current role: test plan, evidence, and run tracking
- Likely user story: "Maintain a test plan for a project phase, record evidence, and track the latest run results."
- Current problems:
  - list has no selected state or filters
  - the editor is long and visually repetitive
  - cases, evidence, and runs all use similar card treatment, so scanning is fatiguing
  - action area is not sticky
  - no summary view of plan health before entering the long editor
- Recommended improvements:
  - add filters by project, phase, and latest result
  - add selected-row styling
  - segment the detail pane into clearly labeled subsections or secondary tabs
  - add summary metrics at the top of the detail pane
  - use sticky actions for save/reset
- Candidate shared components:
  - `ListDetailShell`
  - `FilterBar`
  - `SectionTabs`
  - `SummaryTiles`
  - `StickyActionFooter`
- Complexity / risk: medium
- Priority: high
- Phase: early page migration

## Activity

- Route: `/activity`
- Component: `src/CanDoItAll.Modules.Activity/Pages/ActivityPage.razor`
- Current role: cross-entity search and recent timeline
- Likely user story: "Find something I worked on recently or jump to a related artifact."
- Current problems:
  - search state is under-specified
- no distinction between "no query yet" and "query returned no results"
  - result layout is basic and lacks stronger affordance
  - timeline is readable but could group or summarize better
- Recommended improvements:
  - use a standard search bar and empty state
  - add better no-results handling
  - make result cards more obviously actionable
  - optionally group the timeline by date for faster scanning
- Candidate shared components:
  - `SearchBar`
  - `EmptyState`
  - `TimelineGroup`
  - `ListHeader`
- Complexity / risk: low-medium
- Priority: medium
- Phase: later page migration

## Automation

- Route: `/automation`
- Component: `src/CanDoItAll.Modules.Automation/Pages/AutomationPage.razor`
- Current role: background job visibility
- Likely user story: "See whether asynchronous work succeeded, failed, or is still running."
- Current problems:
  - only one long list view
  - no summary by status
  - no filters by job state or job type
  - page does not clearly guide what to do when a failure appears
- Recommended improvements:
  - add summary tiles and simple filters
  - standardize empty state and error emphasis
  - consider a compact detail pattern for failed jobs only
- Candidate shared components:
  - `SummaryTiles`
  - `FilterBar`
  - `EmptyState`
  - `StatusListItem`
- Complexity / risk: low
- Priority: low-medium
- Phase: later page migration

## Settings

- Route: `/settings`
- Component: `src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor`
- Current role: workspace defaults, secrets, and provider profiles
- Likely user story: "Configure the environment so prompt and resource workflows work reliably."
- Current problems:
  - three different admin jobs are stacked on one long page
  - secrets are both edited and listed inside the same surface
  - provider capability checkboxes are dense and visually flat
  - destructive actions are inline with routine edit actions
  - no secondary navigation to separate admin concerns
- Recommended improvements:
  - add secondary tabs or local section navigation: `Workspace`, `Secrets`, `Providers`
  - split list and editor concerns for secrets and providers
  - group provider capabilities under a dedicated subsection
  - standardize save/clear/delete placement
  - reserve destructive confirmation for later shared dialog work if phase 1 cannot add it safely
- Candidate shared components:
  - `SecondaryTabs`
  - `ListDetailShell`
  - `FormSection`
  - `CapabilityChecklist`
  - `StickyActionFooter`
- Complexity / risk: medium
- Priority: high
- Phase: early to mid-page migration
