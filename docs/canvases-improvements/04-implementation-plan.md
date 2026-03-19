# Canvas Improvements Implementation Plan

## Goal

Deliver a shared canvas system so:

1. `ProjectStructurePage` gains the same interaction depth, visual language, editor layout, and in-canvas tooling proven in the ZyphoNote learning builder canvas.
2. `PromptFactoryPage` is upgraded from a form-first wizard into a canvas-first flow editor that uses the same layout system, controls, cards, inspector, overlays, and modal language.
3. Both canvases run on one reusable foundation instead of duplicating rendering, zoom, pan, selection, chrome, modal, and inspector behavior in separate implementations.

This plan is intentionally sequential. Later phases assume the shared foundation from earlier phases already exists.

---

## Phase 1: Build the shared canvas foundation

### Purpose

Create the reusable substrate that both editors will consume. No page-specific polish should be hardcoded during this phase.

### Primary outcomes

- Introduce a reusable Blazor canvas shell in `CanDoItAll.ComponentKit`.
- Move generic JS canvas behavior out of `workbenchInterop.js` into a shared module with a stable interop API.
- Define shared models for:
  - node descriptors
  - edge descriptors
  - viewport state
  - selection state
  - inspector payloads
  - create-action payloads
  - collapse state
  - manual node positions
- Establish a common canvas event contract for:
  - selection changed
  - nodes moved
  - viewport changed
  - node opened
  - create action invoked
  - help toggled
  - fit requested
  - maximize toggled

### Files expected to change

- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\...` new shared components/models/services
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\wwwroot\js\workbenchInterop.js` split or replaced
- shared CSS assets for the canvas chrome and card system

### Decisions to lock here

- One shared renderer and one shared visual token set for both canvases.
- Shared JS owns geometry, transforms, hit testing, marquee math, and drag logic.
- Blazor owns domain state, editor content, create rules, and persistence.

### Acceptance criteria

- A generic demo instance can render nodes, edges, zoom, pan, fit, select, multi-select, marquee-select, drag, and emit events without any project-structure-specific assumptions.
- Shared API supports right inspector content and overlay modals without page-specific hacks.

### Risks

- If generic contracts are too narrow, later phases will reintroduce duplication.
- If generic contracts are too abstract, the implementation becomes slow to land.

### Mitigation

- Keep the contracts concrete and evidence-driven from the reference behavior already documented in `01-reference-and-gap-analysis.md`.

---

## Phase 2: Apply the shared visual system and chrome

### Purpose

Match the screenshots and reference canvas atmosphere before migrating page-specific logic.

### Primary outcomes

- Replace current canvas styling with the shared visual system:
  - atmospheric gradient background
  - subtle dot/grid texture
  - dark root card treatment
  - bright section cards
  - pastel typed child cards
  - elevated rounded inspector cards
- Build shared top chrome:
  - add button cluster
  - optional focus/current-node action
  - fit button
  - maximize toggle
  - help button
  - zoom out button
  - zoom slider
  - zoom in button
  - zoom percentage pill
- Build shared helper surfaces:
  - bottom-left hint pill
  - in-canvas help overlay/modal
  - shared modal container visuals matching the screenshots

### Files expected to change

- shared canvas CSS/SCSS
- shared Blazor shell layout
- shared modal components if they do not already exist

### Acceptance criteria

- Canvas chrome and layout look visually consistent with the screenshots without relying on page-specific CSS overrides.
- Both pages can opt into the same chrome with configuration rather than custom markup.

### Risks

- Shipping visuals too early without responsive constraints can break inspector layout on smaller widths.

### Mitigation

- Validate desktop and compact widths during this phase before domain migration continues.

---

## Phase 3: Migrate Project Structure editor onto the shared system

### Purpose

Bring `ProjectStructurePage` to parity first because it already has a canvas and therefore has the least conceptual distance from the target state.

### Primary outcomes

- Replace the custom `ProjectStructureCanvas` internals with the shared canvas shell.
- Preserve the current project structure domain and action semantics while upgrading the interaction model.
- Add parity behaviors missing today:
  - marquee selection
  - multi-selection
  - contextual add actions that place new nodes beside the source node
  - fit/maximize/help chrome
  - bottom-left interaction hints
  - branch collapse/expand where applicable
  - inspector styling parity
  - better canvas persistence

### Files expected to change

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Components\ProjectStructureCanvas.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- workbench services or helpers that translate project nodes into shared canvas descriptors

### Required implementation notes

- Preserve existing actions:
  - open
  - branch
  - validate
  - test
  - skip
  - mark used
  - add linked prompt
  - create child
- Update create-child placement rules so new nodes are offset from the source in a deterministic fan or branch pattern.
- Keep graph semantics intact. Parity is for tooling and visual language, not forced tree-only behavior.
- Ensure selected node and selected multi-node inspector states are both supported.

### Acceptance criteria

- User can operate the Project Structure editor with the same interaction vocabulary used by the reference canvas.
- Inspector and node styling no longer feel like a separate product.

### Risks

- Graph edges may visually overlap more than the hierarchical reference.
- Existing JS state persistence may conflict with new shared persistence.

### Mitigation

- Introduce edge routing rules that prefer orthogonal or softened diagonal spacing where possible.
- Migrate persisted state under a new storage key/version.

---

## Phase 4: Convert Prompt Wizard into a canvas-first editor

### Purpose

Promote `PromptFactoryPage` from a stacked form experience into a real flow editor sharing the same canvas system.

### Primary outcomes

- Replace the current list-and-form arrangement with:
  - left canvas stage
  - right inspector/editor panel
  - optional supporting panels beneath or beside the stage where needed
- Map prompt builder concepts into node types:
  - input
  - task
  - rule
  - variable/source
  - branch/decision
  - output
  - run or preview node if the product keeps execution artifacts visible
- Reuse the same card language, create rail, help overlay, modal surfaces, and inspector patterns from Phase 2.
- Preserve existing wizard/business rules while moving editing into inspector-driven forms.

### Files expected to change

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\FactoryDomain.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\PromptFactoryService.cs`
- new mapper/adapters between prompt domain objects and shared canvas descriptors

### Required implementation notes

- The page must still support the current wizard outcomes and saved data.
- Editing must move to the inspector instead of requiring detached forms wherever practical.
- Multi-selection should at minimum support batch movement and shared destructive/organizational actions if business rules allow.
- If some prompt-step configuration is too complex for inline inspector editing, it must still open in a modal that uses the same visual system.

### Acceptance criteria

- Prompt wizard now looks and behaves like a sibling of Project Structure, not a different application.
- Core prompt flow actions can be performed from the canvas without regressing existing validation.

### Risks

- Prompt builder domain may contain linear assumptions that resist graph editing.
- Existing list rendering may encode implicit ordering not yet captured as edge metadata.

### Mitigation

- Introduce explicit sequence/branch metadata before final UI conversion.
- Keep a deterministic layout pass for any nodes lacking manual coordinates.

---

## Phase 5: Add screenshot-specific polish and advanced create flows

### Purpose

Close the final parity gap between a merely functional canvas and the reference-quality editor.

### Primary outcomes

- Implement the screenshot-proven quick-create vertical rail below the main add button.
- Ensure context menus, add surfaces, and inspector spacing align with the screenshots.
- Standardize icon sizing, rounded corners, shadows, typography scale, label pills, and section spacing.
- Align modal visuals with the rounded white step-driven surfaces visible in the screenshots.

### Acceptance criteria

- A side-by-side screenshot comparison reads as the same product family.
- No obvious mismatch remains in chrome, inspector rhythm, card hierarchy, or create affordances.

---

## Phase 6: Persistence, accessibility, and regression testing

### Purpose

Make the new shared system reliable enough for sustained use.

### Primary outcomes

- Persist viewport, selection, collapsed groups, and manual positions using versioned storage keys.
- Add keyboard support for:
  - zoom in
  - zoom out
  - reset/fit
  - help
  - escape/clear selection
- Verify screen-reader labels for toolbar buttons, menus, modals, inspector controls, and canvas node summaries.
- Add automated test coverage where feasible for:
  - descriptor mapping
  - create placement rules
  - collapse rules
  - selection state transitions
  - prompt flow serialization
- Add manual QA scripts for interactive validation.

### Acceptance criteria

- Refreshing the page does not discard intentional workspace state.
- Keyboard and accessible labeling are good enough to ship.
- No major regression in node editing, connection display, or saving behavior.

---

## Recommended work split

### Shared system owner

Responsible for:

- shared canvas shell
- shared JS runtime
- shared tokens and CSS
- shared chrome/help/modal components

### Project Structure owner

Responsible for:

- mapping workbench domain into shared descriptors
- inspector parity for project structure nodes
- graph-specific actions and create placement rules

### Prompt Wizard owner

Responsible for:

- prompt node taxonomy
- prompt inspector/editor migration
- flow serialization and validation

### QA owner

Responsible for:

- screenshot parity review
- checklist execution from `03-parity-checklist.md`
- regression verification across both canvases

---

## Definition of done

The work is done only when all of the following are true:

1. Both editors use the same shared canvas foundation.
2. Both editors present the same visual system and chrome.
3. Both editors support the interaction set documented from the reference canvas.
4. Project Structure preserves its graph-specific behavior while adopting the new system.
5. Prompt Wizard is converted into a canvas-first editor rather than a themed form page.
6. Screenshot-driven QA confirms no major parity gaps remain.
