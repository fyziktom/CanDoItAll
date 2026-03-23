# Implementation Plan

## Delivery Streams

### Stream 1: Interaction polish
- make help popovers dismiss on outside click and delayed mouse leave
- keep styles consistent with the current visual language

### Stream 2: Scroll reduction
- replace the top button strip with real page tabs
- default to `Canvas`
- make `Canvas` the only tab that renders canvas + floating inspector
- move `Setup`, `Governance`, `Assembly`, and `Review` into separate page tabs
- restyle the tab strip so the active tab visually connects to the active panel like a standard browser tab

### Stream 2B: Inspector refocus
- strip page-wide workflow duplication out of the inspector
- keep the inspector focused on selected canvas-item details and actions
- route broad workspace editing into the corresponding page tabs
- make selected prompt components editable directly in the inspector
- add a subtree or selection preview action to the inspector for copy-ready prompt slices

### Stream 2C: Floating inspector
- remove the fixed external inspector column from the canvas layout
- render the inspector inside the canvas surface
- default it to a right-docked floating position
- support drag and reposition within the canvas
- support minimize and restore without leaving the canvas
- keep the same inspector available in maximized canvas mode

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
- [ ] replace the button-strip control with a real tab strip
- [ ] keep tab state in Prompt Factory
- [ ] default support tab to `Canvas`
- [ ] render canvas + floating inspector only on the `Canvas` tab
- [ ] render only the active non-canvas workspace on the other tabs
- [ ] keep current wizard step synced when the user opens a stage tab

### C. Inspector behavior
- [ ] remove the large workflow/stage chooser from the inspector
- [ ] add a contextual inspector header for the selected node
- [ ] keep setup-node actions in the inspector, but move full setup editing to the `Setup` tab
- [ ] keep prompt-step editing in the inspector because it is item-specific
- [ ] keep attachment/component/branch actions in the inspector because they are item-specific
- [ ] show selected component content in a multiline editor that fills the available inspector space
- [ ] store component edits in the session customization model used by prompt build
- [ ] add a preview action that opens a modal for the selected item or subtree
- [ ] keep a reset path so component content can return to its template-based baseline
- [ ] remove redundant generic intro cards from prompt-component and component-group inspector states
- [ ] compress selected-item summary cards so chip density does not push the editor below the fold
- [ ] guarantee selected-item modals layer above the canvas shell, floating inspector, and maximized stage chrome

### C2. Floating inspector behavior
- [ ] remove the external inspector column from the canvas stage when Prompt Factory is on the `Canvas` tab
- [ ] render the contextual inspector as a floating panel inside the canvas surface
- [ ] dock it to the right by default
- [ ] support drag via a dedicated handle
- [ ] support minimize and restore
- [ ] keep drag and minimize working in maximized canvas mode

### D. Setup wizard
- [ ] add a session setup profile model
- [ ] persist setup profile within prompt session data
- [ ] create a canvas node for setup
- [ ] add inspector editing for setup
- [ ] add support-tab editing for setup
- [ ] prefill known values from project data and current session values
- [ ] clearly mark missing required fields

### E. Components toolbox
- [ ] extend action model or menu rendering to support a toolbox panel layout
- [ ] route `Components` into the toolbox panel
- [ ] add search
- [ ] add accordion groups
- [ ] add hover preview
- [ ] keep generic radial menus unchanged for other categories

### F. Attachments
- [ ] improve input prompts to capture extraction intent
- [ ] add richer visual typing for file inputs
- [ ] map common file types to accent and icon treatment
- [ ] exclude setup metadata from normal attachment counts and lists

### G. Safety
- [ ] add confirmation dialog state
- [ ] confirm bulk recommendation changes
- [ ] confirm reset and large clear actions
- [ ] surface change counts in the dialog body

### H. Verification
- [ ] component tests for key Prompt Factory rendering paths
- [ ] Playwright checks for main workbench flow
- [ ] screenshot review for context, setup, components toolbox, and attachments

## Validation Criteria

### Layout
- `Canvas` shows canvas and floating inspector only.
- `Setup`, `Governance`, `Assembly`, and `Review` do not render the canvas.
- Only one page-tab workspace is visible at a time.
- The default canvas tab has no support section below it.
- The active tab looks visually attached to the active content panel.
- The external inspector column is gone.
- The floating inspector defaults to the right side of the canvas.
- The floating inspector can be minimized and restored.
- The floating inspector can be dragged without breaking canvas interaction.
- Component and group inspector states do not render redundant explanatory cards ahead of real content.
- Compact chips and summary spacing keep the editor or selected-items list visible without unnecessary scrolling.

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
- Editing a selected component changes the session override that build uses.
- Previewing a selected prompt node shows that node and its descendants only.
- Expanding the large editor opens a modal above the canvas and floating inspector in both normal and maximized canvas modes.

## Execution Prompts

### Implementation prompt
Build the Prompt Factory refactor in a canvas-first way. Use a real page-tab model where `Canvas` is the first tab and the only tab that renders the canvas plus contextual floating inspector. Move setup, governance, assembly, and review into their own full page tabs. Keep the inspector focused on the selected canvas item, not on duplicating whole workspaces. Render the inspector inside the canvas surface, dock it to the right by default, and let users drag or minimize it so maximized canvas work remains self-contained. Style the tabs like connected browser tabs. When a prompt component is selected, show its effective prompt text in an editable multiline inspector field and store edits in the same session customization data that final build uses. Add a preview action that can open a modal for the selected item or prompt-step subtree.

### QA prompt
Test Prompt Factory as if it were a high-flexibility consumer productivity tool. Focus on orientation, accidental actions, large-scroll fatigue, discoverability of setup, confidence in component picking, clarity of attachment intent, connected-tab readability, and whether selected-item preview or editing in the inspector feels direct and trustworthy.
