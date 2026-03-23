# UI Review Global Findings

## Evidence Base

This review is grounded in:

- real Razor pages and shell files
- module model/service definitions
- component test coverage
- Playwright smoke/regression tests
- existing repo docs for shared components and canvas work

## Cross-Cutting Issues

### 1. The shell currently over-explains the route

Evidence:

- `MainLayout.razor` top bar already renders route title and route description
- each page then renders `PageHeader` immediately inside the page body

Impact:

- vertical space is consumed before the user can act
- hierarchy becomes ambiguous
- task entry points move lower on the page

### 2. The shell right rail is always on, even when it is not relevant

Evidence:

- `MainLayout.razor` always supplies right-rail cards for workbench/context diagnostics
- `AppShell.razor` reserves a second column for that rail at wide breakpoints

Impact:

- standard management pages lose useful width
- focus-heavy pages compete with global secondary content
- page-level information architecture is harder to read

### 3. Navigation disappears below `lg`

Evidence:

- `AppShell.razor` uses `aside class="hidden ... lg:block"`
- there is no alternate mobile/tablet navigation surface in the shell

Impact:

- tablet/mobile users lose primary navigation
- workspace switching becomes inaccessible in smaller viewports
- responsive behavior is incomplete, not just compressed

### 4. Shared components do not yet control the last mile

Evidence:

- all 12 real route pages use `PageHeader`
- many pages use `SectionCard`
- the app still contains 136 raw `<button>` elements across Razor files
- there is no app-level usage of the older shared `Button`, `Card`, `FormField`, `Tabs`, or `Stack` primitives in route pages

Impact:

- page teams improvise actions, lists, and form rows
- similar screens do not feel related
- polishing one page does not improve the next page

### 5. Existing shared libraries are split and semantically blurry

Current layers:

- `CanDoItAll.Components`: low-level primitives, several of them intentionally thin
- `CanDoItAll.ComponentKit`: shell/workbench/page composites

Observed problem:

- the lower-level library looks more complete than it really is
- the higher-order library is newer and more useful for current pages
- pages still hand-build patterns that should live in one shared layer

Impact:

- implementation agents can pick the wrong layer
- app pages drift toward raw Tailwind markup
- the system lacks a clear composition owner

### 6. Primary actions are buried too low

Evidence:

- `PageHeader` supports `Actions`
- no current route page uses `PageHeader.Actions`
- primary actions are usually placed inside the first `SectionCard`

Impact:

- users must scan into the content to find the first action
- pages feel passive instead of task-oriented

### 7. Standard list/detail pages lack shared selection and filtering patterns

Affected pages:

- Projects
- Resources
- Prompt Gallery
- Validation Center
- Test Lab
- Settings

Common problems:

- selected item is not visually persistent in the list
- list headers lack count/filter/search affordances
- row-level actions are inconsistent
- the user can lose track of which record is currently open

### 8. Form-heavy pages are long but not structured enough

Evidence:

- 9 route pages use `EditForm`
- long forms are mostly rendered as one continuous stack of bespoke fields
- save/reset/delete regions are not sticky or standardized

Impact:

- scanning cost is high
- destructive actions sit too close to routine actions
- the user has to remember where save controls live

### 9. Empty/loading/error states are mostly ad hoc

Evidence:

- empty states are page-local dashed blocks or simple paragraphs
- loading states on workbench pages are plain text only
- there is no obvious shared error-state pattern beyond inline text messages

Impact:

- state changes feel inconsistent
- the user gets little guidance on what to do next

### 10. Home is informative, not operational

Evidence:

- `Home.razor` contains explanatory cards only
- there are no direct CTA buttons to start or resume work

Impact:

- the dashboard behaves like project documentation instead of a command surface

### 11. The shell emphasizes system context more than user flow

Evidence:

- workspace switcher, route navigation, open projects, prompt sessions, tab strip, active tab summary, and right rail are all present before page content settles

Impact:

- the shell feels assembled from useful subsystems rather than organized around the current task

## Repeated Anti-Patterns

- duplicate titles/descriptions
- action buttons inside cards instead of in page headers
- repeated raw form-field markup with one-off spacing
- repeated raw list item cards without selected state
- repeated success messages with no shared feedback pattern
- nested card treatment for sub-sections that should use lighter subdivisions
- global secondary information shown on every page, not only when useful

## Design Debt Themes

### Theme A: structural inconsistency, not visual inconsistency

The main issue is not color chaos. The problem is that the app lacks a stable page grammar.

### Theme B: page-local convenience beats reusable composition

Pages do the quickest thing locally, so the system never gets stronger globally.

### Theme C: advanced workbench routes and standard CRUD routes have diverged

The canvas routes are highly intentional. The standard pages are still largely utility pages.

## Information Architecture Issues

- route identity appears in too many places
- standard pages do not clearly separate navigation, selection, editing, and system state
- some pages mix setup, editing, and review inside one large surface without sectional hierarchy
- workspace selection currently changes default navigation destination, but not the visible navigation model itself

## Interaction Consistency Issues

- list items sometimes act as selectors, sometimes as launchers, sometimes both
- save/reset/delete actions move around by page
- row actions are sometimes inline buttons and sometimes only route changes
- there is no consistent place for filters or list counts
- there is no standard sticky action region for long forms

