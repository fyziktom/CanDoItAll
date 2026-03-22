# Implementation Plan

## Delivery Streams

### Stream 1: Interaction polish
- make help popovers dismiss on outside click and delayed mouse leave
- keep styles consistent with the current visual language

### Stream 2: Scroll reduction
- add a top-level support-lane tab strip
- default to `Canvas`
- move lower support content behind `Setup`, `Governance`, `Assembly`, and `Review` tabs

### Stream 3: Setup wizard
- create a persistent session setup model
- expose it as a canvas-visible setup node
- prefill from project when possible
- make it editable later from inspector and support tab

### Stream 4: Prompt-component toolbox
- keep radial menu for generic actions
- switch prompt components to a toolbox panel submenu
- add search, accordion grouping, and hover preview

### Stream 5: Attachment UX
- improve input copy so users specify extraction intent
- style nodes by file kind or extension
- keep generic file upload support

### Stream 6: Safety
- add confirmation for bulk add or reset actions
- show impact counts
- preserve undo and redo

## Implementation Checklist

### A. Bundle and product contract
- [x] rewrite raw feedback into structured requirements
- [x] define user stories and intended UX behavior
- [x] define diagrams and layout proposals

### B. Page structure
- [ ] add support-lane tab state to Prompt Factory
- [ ] default support tab to `Canvas`
- [ ] render only the active support lane below the canvas
- [ ] keep current wizard step synced when the user opens a stage tab

### C. Setup wizard
- [ ] add a session setup profile model
- [ ] persist setup profile within prompt session data
- [ ] create a canvas node for setup
- [ ] add inspector editing for setup
- [ ] add support-tab editing for setup
- [ ] prefill known values from project data and current session values
- [ ] clearly mark missing required fields

### D. Components toolbox
- [ ] extend action model or menu rendering to support a toolbox panel layout
- [ ] route `Components` into the toolbox panel
- [ ] add search
- [ ] add accordion groups
- [ ] add hover preview
- [ ] keep generic radial menus unchanged for other categories

### E. Attachments
- [ ] improve input prompts to capture extraction intent
- [ ] add richer visual typing for file inputs
- [ ] map common file types to accent and icon treatment
- [ ] exclude setup metadata from normal attachment counts and lists

### F. Safety
- [ ] add confirmation dialog state
- [ ] confirm bulk recommendation changes
- [ ] confirm reset and large clear actions
- [ ] surface change counts in the dialog body

### G. Verification
- [ ] component tests for key Prompt Factory rendering paths
- [ ] Playwright checks for main workbench flow
- [ ] screenshot review for context, setup, components toolbox, and attachments

## Validation Criteria

### Layout
- Canvas tab shows canvas and inspector without forcing the lower workspace open.
- Only one lower support lane is visible at a time.
- The page is meaningfully shorter on the default view.

### Help behavior
- Clicking outside closes help.
- Hovering out closes help after a short delay.
- Re-entering before the delay cancels close.

### Setup wizard
- New blank sessions show a setup entry point.
- Project-backed sessions prefill what is known.
- Missing fields are visible and editable later.
- Setup remains reachable in maximized canvas flow.

### Toolbox
- Components are searchable.
- Groups are collapsible.
- Hover preview appears for items.
- Selecting a component adds only that component unless the user explicitly confirms a bulk action elsewhere.

### Attachments
- Any file can still be attached.
- Common file types are visually differentiated.
- Extraction intent is captured and visible.

### Safety
- Bulk changes show confirmation.
- Reset actions warn before clearing.
- Undo still works after confirmed actions.

## Execution Prompts

### Implementation prompt
Build the Prompt Factory refactor in a canvas-first way. Keep the canvas and inspector dominant, move lower support content behind tabs, add a persistent setup node and editable setup wizard, replace component radial browsing with a searchable toolbox panel while preserving radial menus for generic actions, improve attachment semantics and styling, and add confirmation for heavy changes.

### QA prompt
Test Prompt Factory as if it were a high-flexibility consumer productivity tool. Focus on orientation, accidental actions, large-scroll fatigue, discoverability of setup, confidence in component picking, and clarity of attachment intent.
