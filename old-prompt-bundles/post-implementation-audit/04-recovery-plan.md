# 04 - Recovery Plan

This is the recommended repair sequence.

## Phase 0 - Freeze the target

### Outcome

Lock the intended product shape before adding more breadth.

### Tasks

- treat this audit as authoritative recovery guidance
- avoid adding new feature breadth until shell, workbench, wizard, and manager gaps are closed
- record any deliberate deviations as ADRs

### Gate

Proceed only when the team agrees that the current product target is:

- wizard-first
- canvas-enabled
- artifact-tab driven
- real-engine based

## Phase 1 - Shell and workbench semantics

### Outcome

Turn the shell into a real workstation.

### Tasks

- upgrade the left rail and shell context model
- promote artifact/session tab kinds
- add opened-project and opened-session semantics
- add overflow and batch tab actions
- keep browser storage restore

### Gate

Proceed only when:

- tabs are artifact-aware
- opened projects and prompt sessions are first-class tabs
- left navigation is no longer just a flat route list

## Phase 2 - Unified project object domain

### Outcome

Create the graph model the workbench will author.

### Tasks

- add the shared project-object contract
- define object types, connections, and visual profiles
- map existing entities into the graph model
- prepare typed commands for create, link, move, edit, branch, validate, and test

### Gate

Proceed only when:

- the project structure surface can be backed by a real authoring graph
- object kinds and visuals are explicit

## Phase 3 - Project wizard recovery

### Outcome

Project creation and editing become guided workflows.

### Tasks

- replace raw CRUD as the primary project UX
- add wizard steps and guidance
- allow modal and dedicated-tab variants
- connect project creation completion to the workbench graph

### Gate

Proceed only when:

- new project creation is wizard-first
- project editing is comfortable and staged

## Phase 4 - Real structure canvas and calendar

### Outcome

Replace placeholder wrappers with real engine integrations.

### Tasks

- integrate the documented canvas engine
- integrate the documented calendar engine
- persist viewport, selection, node positions, and view preferences
- implement the grouped hex right-click menu
- open linked artifacts into internal tabs

### Gate

Proceed only when:

- the structure surface is a real canvas
- the calendar is a real calendar
- node creation and linking work through typed C# commands

## Phase 5 - Prompt wizard and flow recovery

### Outcome

Prompt work becomes a session-based visual workflow.

### Tasks

- add prompt session tabs
- move prompt creation into a stepper workflow
- expose shared block governance and flow-template governance
- connect prompt nodes into the project graph
- support parallel branches and visible lineage

### Gate

Proceed only when:

- prompt wizard sessions reopen correctly
- prompt steps and branches are visible in the workbench
- shared block rules are centrally manageable

## Phase 6 - Typed resource editors and cross-module UX

### Outcome

Resource management, card UX, and wizard polish become consistent.

### Tasks

- introduce typed resource editors
- enforce card-first list surfaces
- standardize modal and dedicated-tab wizard patterns
- tighten top-bar, right-rail, empty-state, and action-bar consistency

### Gate

Proceed only when:

- the product no longer relies on generic JSON editing as the main resource UX
- cross-module interaction patterns feel consistent

## Phase 7 - Manager and tuning completion

### Outcome

The adaptive tuning loop is production-shaped for development use.

### Tasks

- replace fake tuning execution with a real adapter
- support screenshot and clipboard image input
- connect tuning records to watch-ready and capsule-drift checks
- expose useful status history for debugging

### Gate

Proceed only when:

- tuning requests execute through a real adapter
- ready-for-review means actual watch-ready plus no capsule drift

## Phase 8 - QA closeout and release gate

### Outcome

The repaired system is safe to continue product development on.

### Tasks

- fix the Playwright cleanup failure
- extend automated coverage for repaired flows
- run manual UX walkthroughs for the main user journeys
- compare the product again against this audit

### Final release gate

Do not call the recovery complete until all of the following are true:

- structure canvas is real
- calendar is real
- projects are wizard-driven
- prompt sessions are tab-driven and graph-visible
- unified project object model exists
- tuning loop is real, not simulated
- shell navigation reflects the intended workstation model
