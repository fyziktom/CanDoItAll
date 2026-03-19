# Sequential Prompts For The Implementation Agent

Use these prompts in order. Do not skip ahead. Each prompt assumes the previous one is complete.

---

## Prompt 1: Build the shared canvas foundation

### Prompt

Read these documents first:

- `C:\repositories\CanDoItAll\docs\canvases-improvements\01-reference-and-gap-analysis.md`
- `C:\repositories\CanDoItAll\docs\canvases-improvements\02-shared-canvas-system-spec.md`
- `C:\repositories\CanDoItAll\docs\canvases-improvements\03-parity-checklist.md`

Read these current implementation files:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Components\ProjectStructureCanvas.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\wwwroot\js\workbenchInterop.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`

Implement a shared reusable canvas system in `CanDoItAll.ComponentKit` that can support both Project Structure and Prompt Wizard. The shared system must provide:

- node and edge descriptor models
- viewport model and persistence hooks
- selection model with single-select and multi-select
- JS interop for zoom, pan, marquee, drag, fit, and pointer-centered wheel zoom
- event callbacks for node open, selection change, move, create action, help, maximize, and viewport change

Do not migrate either page fully yet. Deliver the shared foundation and one small host example proving the API works.

### Deliverables

- new shared components/models/services
- shared JS module replacing or superseding workbench-specific canvas logic
- buildable host usage example

### Verification

- confirm a generic sample can render nodes and edges
- confirm zoom/pan/drag/multi-select/marquee work
- confirm interop events round-trip to Blazor

---

## Prompt 2: Apply the shared visual system and canvas chrome

### Prompt

Read these documents first:

- `C:\repositories\CanDoItAll\docs\canvases-improvements\02-shared-canvas-system-spec.md`
- `C:\repositories\CanDoItAll\docs\canvases-improvements\03-parity-checklist.md`
- `C:\repositories\CanDoItAll\docs\canvases-improvements\04-implementation-plan.md`

Using the shared foundation from the previous step, implement the visual system and global chrome so the shared canvas matches the ZyphoNote reference family. Build:

- atmospheric canvas background
- rounded card hierarchy for root, group, and typed item nodes
- right inspector shell
- top-right zoom rail with fit/maximize/help controls
- top-left add cluster
- bottom-left hint pill
- shared help overlay/modal
- shared modal styling matching the screenshot language

Keep the system configurable, but the default should already look like the reference.

### Deliverables

- shared CSS/tokens/theme variables
- shared toolbar/chrome components
- shared help/modal surfaces

### Verification

- visually compare against the screenshots in `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\screenshots`
- verify responsive layout does not collapse badly on narrower widths
- verify buttons have accessible labels

---

## Prompt 3: Migrate Project Structure onto the shared canvas

### Prompt

Read these documents first:

- `C:\repositories\CanDoItAll\docs\canvases-improvements\01-reference-and-gap-analysis.md`
- `C:\repositories\CanDoItAll\docs\canvases-improvements\03-parity-checklist.md`
- `C:\repositories\CanDoItAll\docs\canvases-improvements\04-implementation-plan.md`

Read and update these files:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Components\ProjectStructureCanvas.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`

Replace the current Project Structure canvas internals with the shared canvas system. Preserve existing domain behaviors, but add parity with the reference where missing:

- multi-selection
- marquee selection
- improved add-next-to-source placement
- fit/maximize/help controls
- bottom-left usage hints
- better inspector layout and styling
- collapse/expand support where the structure allows it
- versioned canvas state persistence

Keep graph semantics. Do not force the editor into a strict tree if the project structure domain needs graph links.

### Deliverables

- migrated project structure canvas
- descriptor mapping layer from workbench domain to shared nodes/edges
- updated inspector/editor layout

### Verification

- verify current project actions still work
- verify new child nodes do not land directly on top of the source node
- verify zoom/pan/select/drag/help/maximize all function in the page

---

## Prompt 4: Add Project Structure parity polish and advanced create flows

### Prompt

Read these documents first:

- `C:\repositories\CanDoItAll\docs\canvases-improvements\03-parity-checklist.md`
- `C:\repositories\CanDoItAll\docs\canvases-improvements\04-implementation-plan.md`
- `C:\repositories\CanDoItAll\docs\canvases-improvements\06-qa-senior-review.md` once it exists in your branch

Refine the Project Structure editor until it matches the screenshot and reference quality more closely. Focus on:

- create rail behavior under the add button
- node spacing rhythm
- connection readability
- inspector spacing and grouping
- modal styling
- selection summary behavior for multi-select

If some create actions do not map one-to-one from the learning builder reference, preserve the visual system and interaction shape while adapting labels and actions to the project structure domain.

### Deliverables

- polished project structure canvas with screenshot-level parity

### Verification

- compare the resulting UI against the screenshot set
- confirm no visually obvious mismatch remains in chrome, inspector shell, or card system

---

## Prompt 5: Convert Prompt Wizard into a canvas-first editor

### Prompt

Read these documents first:

- `C:\repositories\CanDoItAll\docs\canvases-improvements\01-reference-and-gap-analysis.md`
- `C:\repositories\CanDoItAll\docs\canvases-improvements\02-shared-canvas-system-spec.md`
- `C:\repositories\CanDoItAll\docs\canvases-improvements\04-implementation-plan.md`

Read and update these files:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\FactoryDomain.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\PromptFactoryService.cs`

Turn Prompt Wizard into a canvas-first editor using the shared canvas system. Introduce a prompt-flow node model and map the existing domain into canvas nodes and edges. The page must now use:

- left canvas stage
- right inspector/editor
- shared toolbar/chrome
- canvas-driven selection and editing
- shared modal system for any advanced editing that cannot remain inline

Preserve existing validation and save behavior.

### Deliverables

- migrated prompt wizard canvas page
- prompt node taxonomy and descriptor mapping
- inspector-driven editing workflow

### Verification

- verify the page still supports its existing outcomes
- verify prompt flow nodes can be added, moved, selected, edited, and connected according to the chosen domain rules

---

## Prompt 6: Add Prompt Wizard parity polish and supporting panels

### Prompt

Read these documents first:

- `C:\repositories\CanDoItAll\docs\canvases-improvements\03-parity-checklist.md`
- `C:\repositories\CanDoItAll\docs\canvases-improvements\04-implementation-plan.md`
- `C:\repositories\CanDoItAll\docs\canvases-improvements\06-qa-senior-review.md`

Refine Prompt Wizard so it reads as the same product family as Project Structure and the ZyphoNote reference. Focus on:

- inspector grouping and spacing
- prompt node card styles
- create-action vocabulary and quick-create rail
- help overlay content
- any lower support panels needed for previews, run results, or detail views

Do not reintroduce a form-first page layout. Keep the canvas as the primary workspace.

### Deliverables

- polished prompt wizard canvas with shared layout and visual parity

### Verification

- compare Project Structure and Prompt Wizard side by side
- confirm they now clearly share one canvas system

---

## Prompt 7: Finish screenshot-driven create rail and modal parity

### Prompt

Read these assets and docs first:

- all screenshots under `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\screenshots`
- `C:\repositories\CanDoItAll\docs\canvases-improvements\01-reference-and-gap-analysis.md`
- `C:\repositories\CanDoItAll\docs\canvases-improvements\03-parity-checklist.md`

Perform a screenshot-driven visual and interaction refinement pass across both canvases. Match:

- quick-create vertical rail presentation
- rounded modal shell
- step-pill header feel where applicable
- spacing, shadows, and color balance
- icon sizing and label density

This step is about removing the final “close but not the same” inconsistencies.

### Deliverables

- both canvases polished to a consistent screenshot-informed finish

### Verification

- capture before/after screenshots
- note any remaining intentional deviations and why they are necessary

---

## Prompt 8: Run final QA and regression closure

### Prompt

Read these documents first:

- `C:\repositories\CanDoItAll\docs\canvases-improvements\03-parity-checklist.md`
- `C:\repositories\CanDoItAll\docs\canvases-improvements\06-qa-senior-review.md`

Execute the parity checklist and produce a completion report. Validate:

- interaction parity
- visual parity
- inspector parity
- project structure functional parity
- prompt wizard functional parity
- persistence behavior
- keyboard/help/accessibility basics

Fix any P1/P2 gaps discovered during verification before closing.

### Deliverables

- completed checklist
- QA summary with remaining risks, if any

### Verification

- both editors build cleanly
- key flows work manually
- no blocking parity gaps remain
