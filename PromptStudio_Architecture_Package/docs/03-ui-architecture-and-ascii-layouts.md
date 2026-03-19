# 03 — UI Architecture, Reusable Components, and ASCII Layouts

## 1. UI objectives

The UI must make a large functional scope feel coherent. The design must support:
- fast navigation across many projects
- strong project context awareness
- structured prompt workflows
- high-density technical detail when needed
- safe handling of sensitive data
- progressive disclosure of advanced capabilities
- future module growth without a redesign

## 2. UX architecture principles

1. **One shell, many workspaces**  
   All modules live under one consistent application shell.

2. **Project-centric navigation**  
   Most work should start from a project or a phase.

3. **Typed resource interactions**  
   Resource forms and detail panels must adapt to the resource type.

4. **Factory-first prompt generation**  
   Prompt creation should be guided and contextual, not a blank page by default.

5. **Validation as a first-class area**  
   Reviews, checks, and evidence must not be hidden as secondary features.

6. **Consistency over cleverness**  
   Lists, details, forms, side panels, badges, and action bars should behave the same way in every module.

## 3. Information architecture

## 3.1 Global navigation
- Dashboard
- Projects
- Prompt Gallery
- Prompt Factory
- Validation Center
- Test Lab
- Settings

## 3.2 Project workspace navigation
- Overview
- Stack Profile
- Resources
- Prompts
- Architecture
- Plan
- Validation
- Test Evidence
- Activity

## 3.3 Global utility surfaces
- universal search
- notifications/toasts
- status bar
- background task drawer
- right-side detail drawer
- command palette (future-ready)
- provider connection status
- secret warnings / redaction hints

## 4. UI zones and layout model

The application uses a consistent 4-zone layout:

1. **Left rail**
   - global navigation
   - workspace/project switcher
   - quick actions

2. **Top bar**
   - breadcrumbs
   - current project / phase
   - search
   - provider status
   - settings access

3. **Main content area**
   - page-specific list/detail/workflow content

4. **Right utility panel**
   - actions
   - validation summary
   - metadata
   - quick links
   - contextual help
   - save/send/export controls

This pattern keeps the UI unified and scales well as modules grow.

## 5. Reusable component architecture

The UI should rely on the existing component set first, then extend it with application-specific components.

## 5.1 Base shell components
- `AppShell`
- `AppSidebar`
- `AppTopbar`
- `PageHeader`
- `BreadcrumbTrail`
- `SectionCard`
- `StatusBadge`
- `TagChip`
- `ActionToolbar`
- `RightDrawer`
- `SplitPane`
- `EmptyState`
- `ErrorState`
- `LoadingBlock`

## 5.2 Form and editor components
- `SmartForm`
- `FieldGroup`
- `OptionSelector`
- `OptionMatrix`
- `NoteField`
- `DateRangeEditor`
- `SecretReferencePicker`
- `ResourceTypePicker`
- `PromptEditor`
- `PromptSectionEditor`
- `ValidationChecklistPanel`
- `ModelProviderPicker`

## 5.3 Collection and detail components
- `ProjectCard`
- `ProjectPhaseTimeline`
- `ResourceList`
- `ResourceDetailPanel`
- `PromptCard`
- `PromptUsageTimeline`
- `ValidationRunCard`
- `TestEvidenceCard`
- `ActivityTimeline`
- `HealthCheckBadge`

## 5.4 Workflow components
- `WizardStepper`
- `ContextAssemblerPanel`
- `PromptBlueprintSelector`
- `GeneratedPromptPreview`
- `ApprovalGatePanel`
- `ReviewDiffViewer`
- `CoverageMatrixView`
- `ExecutionPreviewPanel`

## 5.5 Type-specific resource components
- `FolderResourceEditor`
- `FileResourceEditor`
- `WebLinkEditor`
- `FtpProfileEditor`
- `SshProfileEditor`
- `RepositoryResourceEditor`
- `PowerShellScriptEditor`
- `DockerResourceEditor`
- `SecretResourceLinkEditor`
- `PromptLinkEditor`

## 6. State model for the UI

The UI should distinguish:
- page state
- local form state
- background operation state
- project context state
- transient notifications
- persisted filters/sorting preferences

### Recommended state approach
- use component-local state for simple forms
- use scoped feature state for the current page/workflow
- use lightweight app-wide state containers only for shell-level needs
- do not centralize all state into one giant client store

## 7. Main screens

## 7.1 Dashboard
Purpose:
- quick access to recent projects
- current phase visibility
- prompt activity summary
- pending validations
- connector/provider health
- recommended next actions

### Primary content
- recent projects list
- current-phase cards
- prompt draft summary
- validation queue
- provider health widgets
- background task queue summary

### Actions
- new project
- open prompt factory
- run validation
- open settings

## 7.2 Projects list
Purpose:
- browse, filter, and manage projects

### Filters
- status
- current phase
- primary language
- UI stack
- storage strategy
- provider usage
- updated date

## 7.3 Project overview
Purpose:
- show project identity, phase timeline, stack profile summary, recent prompts, recent validations, and related resources

## 7.4 Resources page
Purpose:
- manage all project-linked resources in one consistent flow

### Required behaviors
- resource type tabs or filters
- add resource action
- validation status
- indexing status
- preview support indicator
- sensitivity marker
- quick access to notes and secret references

## 7.5 Prompt gallery
Purpose:
- search, filter, tag, organize, and reuse prompts across projects

### Views
- all prompts
- drafts
- templates
- blueprints
- final prompts
- collections
- recent usage

## 7.6 Prompt factory
Purpose:
- assemble context and generate a new prompt through a guided wizard

### Steps
1. choose project
2. choose phase
3. choose blueprint
4. select resources and options
5. review assembled context
6. edit generated prompt
7. validate
8. save/send/export

## 7.7 Validation center
Purpose:
- run structured validation workflows for stories, layouts, architecture, plans, prototype, and tests

### Main sections
- validation queue
- checklist catalog
- latest findings
- coverage matrix
- approval gates
- linked evidence

## 7.8 Test lab
Purpose:
- manage UI testing, screenshot evidence, Playwright plans, and validation results

### Main sections
- planned tests
- implemented tests
- latest runs
- screenshot evidence
- coverage by story/feature/phase

## 7.9 Settings
Purpose:
- workspace defaults, providers, database, storage root, secret vault, safety policies, feature flags

## 8. ASCII layouts

## 8.1 Global shell

```text
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ PromptStudio | Workspace: Local Architect Lab | Search ... | Provider: OpenAI ✓ Ollama ✓ │
├───────────────┬───────────────────────────────────────────────────────────┬────────────────┤
│ Dashboard     │ Breadcrumbs: Projects / Alpha / Overview                 │ Quick Actions  │
│ Projects      │ Page Title                                               │ Save           │
│ Prompt Gallery│ Short page summary                                       │ Validate       │
│ Prompt Factory├───────────────────────────────────────────────────────────┤ Export         │
│ Validation    │                                                           │ Send           │
│ Test Lab      │                 Main Content Area                         │ Metadata       │
│ Settings      │                                                           │ Related Links  │
│               │                                                           │ Help           │
│               │                                                           │                │
├───────────────┴───────────────────────────────────────────────────────────┴────────────────┤
│ Background Tasks: 2 running | Notifications | Health | Version | Storage root | DB mode  │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

## 8.2 Dashboard

```text
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ Dashboard                                                                                  │
├───────────────────────────────┬───────────────────────────────┬────────────────────────────┤
│ Recent Projects               │ Current Phase Focus           │ Provider / Connector Health│
│ - Alpha (Architecture)        │ - Architecture reviews: 2     │ OpenAI        Healthy      │
│ - Beta (Implementation)       │ - Test plans pending: 1       │ Ollama Local  Healthy      │
│ - Gamma (Validation)          │ - Draft prompts: 4            │ SSH Profiles  1 warning    │
├───────────────────────────────┴───────────────────────────────┴────────────────────────────┤
│ Recommended Next Actions                                                                    │
│ [Create project] [Open prompt factory] [Review architecture] [Plan tests]                  │
├────────────────────────────────────────────────────────────────────────────────────────────┤
│ Activity Timeline                                                                           │
│ - Prompt “Architecture Review v3” used on Alpha at 11:42                                   │
│ - Validation run “UX Layout Check” completed                                                │
│ - Repository resource revalidated                                                           │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

## 8.3 Project overview

```text
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ Project: Alpha Platform                                                                     │
│ Status: Active | Current Phase: Architecture | Start: 2026-03-01 | Target End: 2026-06-30│
├────────────────────────────────────────────────────────────────────────────────────────────┤
│ Description                                                                                │
│ A modular local-first prompt and delivery orchestration workspace.                         │
├───────────────────────────────┬───────────────────────────────┬────────────────────────────┤
│ Stack Profile                 │ Phase Timeline                │ Key Metrics                │
│ Primary: C#                   │ Discovery      ✓              │ Resources: 18             │
│ Secondary: TS                 │ UX             ✓              │ Draft Prompts: 6          │
│ DB: PostgreSQL                │ Architecture   ▶              │ Open Findings: 3          │
│ UI: Blazor Server             │ Plan           ○              │ Test Evidence: 7          │
├───────────────────────────────┴───────────────────────────────┴────────────────────────────┤
│ Recent Prompts | Recent Validations | Recent Resources                                     │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

## 8.4 Resources page

```text
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ Project Resources                                                   [+ Add Resource]       │
├──────────────────────┬───────────────────────────────────────────────┬──────────────────────┤
│ Filters              │ Resource List                                 │ Detail / Actions     │
│ [All] [Files] [Repo] │ Name           Type        Status   Sensitive │ Name: Alpha Repo     │
│ [Links] [SSH] [FTP]  │ Alpha Repo     Repository  Valid    No        │ Path/URL: ...        │
│ [Secrets] [Scripts]  │ Prod SSH       SSH         Warning  Yes       │ Health: Healthy      │
│ [Docker] [Prompts]   │ UX Spec        Markdown    Indexed  No        │ Tags: repo, core     │
│                      │ Deployment Doc  PDF         Preview  No        │ Notes: ...           │
│ Search...            │ Secrets Vault   SecretRef   Locked   Yes       │ [Validate] [Edit]    │
│                      │                                               │ [Index] [Open]      │
├──────────────────────┴───────────────────────────────────────────────┴──────────────────────┤
│ Preview / Metadata / Validation History / Linked Prompts                                   │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

## 8.5 Prompt gallery

```text
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ Prompt Gallery                                                     [+ New Prompt]          │
├──────────────────────┬───────────────────────────────────────────────┬──────────────────────┤
│ Filters              │ Prompt List                                   │ Detail / Usage       │
│ [All] [Drafts]       │ Title                 Kind       Phase        │ Title: UX Review v2  │
│ [Templates] [Final]  │ Architecture v3       Final      Architecture │ Tags: ux, review     │
│ [Blueprints]         │ Feature Plan Factory  Blueprint  Plan         │ Version: 3           │
│ Project...           │ Test Coverage Draft   Draft      Testing      │ Used in: Alpha       │
│ Phase...             │ UI Layout Validator   Template   UX           │ Repo: alpha/web      │
│ Tags...              │                                               │ Commit: a1b2c3       │
│ Search...            │                                               │ [Clone] [Use] [Edit] │
└──────────────────────┴───────────────────────────────────────────────┴──────────────────────┘
```

## 8.6 Prompt factory

```text
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ Prompt Factory                                                                             │
├────────────────────────────────────────────────────────────────────────────────────────────┤
│ Step 1: Project  →  Step 2: Phase  →  Step 3: Blueprint  →  Step 4: Context  →  Review   │
├──────────────────────────────┬─────────────────────────────────────────────────────────────┤
│ Context Inputs               │ Generated Prompt Preview                                     │
│ Project: Alpha               │ You are implementing...                                      │
│ Phase: Architecture Review   │                                                               │
│ Blueprint: ArchReview-01     │ [editable prompt body]                                       │
│ Resources: repo, spec, pdf   │                                                               │
│ Options: C#, PostgreSQL      │                                                               │
│ Provider: OpenAI             │                                                               │
│ Model: ...                   │                                                               │
│ Validation Warnings: 1       │                                                               │
├──────────────────────────────┴─────────────────────────────────────────────────────────────┤
│ [Back] [Save Draft] [Save Final] [Copy] [Export] [Send to Provider]                        │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

## 8.7 Validation center

```text
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ Validation Center                                                                          │
├──────────────────────┬───────────────────────────────────────────────┬──────────────────────┤
│ Validation Types     │ Validation Runs                               │ Findings / Actions   │
│ Stories              │ UX Story Set v1        Passed with warnings   │ Finding 01           │
│ Layouts              │ Layout Review v2       Failed                 │ Missing flow for...  │
│ Architecture         │ Architecture Review    Passed                 │ Severity: High       │
│ Plan                 │ Plan Validation        Pending                │ Required action: ... │
│ Prototype            │ Prototype Check        Pending                │ Owner: Architect     │
│ Tests                │ Coverage Plan          Draft                  │ [Approve] [Reject]   │
├──────────────────────┴───────────────────────────────────────────────┴──────────────────────┤
│ Coverage Matrix / Linked Evidence / Decision Log                                           │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

## 8.8 Test lab

```text
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ Test Lab                                                                                   │
├──────────────────────┬───────────────────────────────────────────────┬──────────────────────┤
│ Plans / Suites       │ Test Items                                     │ Evidence / Results   │
│ Planned Coverage     │ PW-001 Project creation     Implemented        │ Last Run: Passed     │
│ Playwright Suites    │ PW-002 Add resource         Failed             │ Screenshots: 3       │
│ Screenshot Evidence  │ PW-003 Prompt wizard        Planned            │ Trace: available     │
│ Accessibility        │ A11Y-001 Dashboard          Implemented        │ Linked Story: US-11  │
│ Smoke Tests          │ SMK-001 Startup             Passed             │ [Open report]        │
└──────────────────────┴───────────────────────────────────────────────┴──────────────────────┘
```

## 9. Page behavior rules

### 9.1 Every main page should have
- page header
- short description
- primary actions
- filters where applicable
- list/detail or workflow structure
- right-side context drawer
- empty states
- error states
- loading states

### 9.2 Sensitive views should have
- redacted default display
- explicit reveal action where appropriate
- no value copy by accident
- access warnings before export/send

### 9.3 Validation views should have
- deterministic checklist summary
- open findings grouped by severity
- linked sources or artifacts
- explicit decision actions

## 10. Design system rules

1. Use a restrained visual language.
2. Favor clear spacing and grouping over decoration.
3. Use status badges consistently:
   - green: healthy/passed/ready
   - yellow: warning/draft/pending
   - red: failed/blocked/danger
   - gray: inactive/archived/not available
4. Use the same action order everywhere:
   - Save
   - Validate
   - Export
   - Send / Execute
5. Make phase and status visible in all relevant headers.
6. Use tags and badges rather than dense inline metadata paragraphs.

## 11. Validation of layouts against stories and use cases

### Layout coverage summary
- Dashboard supports US-011, US-027, US-043, US-045.
- Project overview supports US-006 through US-011.
- Resources page supports US-012 through US-022.
- Prompt gallery supports US-023 through US-029.
- Prompt factory supports US-030 through US-036.
- Validation center supports US-037 through US-043.
- Test lab supports US-044 through US-047.

### UI gap check
No requested major capability is missing from the proposed page inventory.

## 12. UI architecture conclusion

The UI should be implemented as:
- one shell
- page-level feature modules
- reusable shared components
- typed editors for resource kinds
- wizard-based prompt factory
- dedicated validation center
- dedicated test lab
- consistent list/detail/action pattern

This structure gives the user a practical, scalable workstation rather than a disconnected set of tools.