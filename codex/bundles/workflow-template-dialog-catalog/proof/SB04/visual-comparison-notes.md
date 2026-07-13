# Visual Comparison Notes

## Catalogue Dialog

- Proposal: wide modal with lazy-load framing, searchable/list catalogue on the left, selected template details on the right, and Preview buttons.
- Implementation: matches the structure using existing `Dialog`, `Grid`, `Stack`, `SurfaceCard`, `StatusBadge`, and `Button` components.
- Screenshot proof: `bundle://proof/SB04/browser/workflow-template-catalogue-dialog-large-offer-filter.png`.
- Accepted differences: the live UI uses the product's existing BaseLib chrome, type scale, and icon styling instead of reproducing the proposal pixel-for-pixel.
- Result: close enough for the requested UX intent. Templates are out of the tab flow and only loaded when the catalogue opens.

## Preview Dialog

- Proposal: full-width dialog with canvas-dominant layout, metadata rail, selected-node details, and Add to my drafts.
- Implementation: matches the layout with a left metadata/selected-node rail, a dominant `CanvasWorkbench` preview stage, and footer `Add to my drafts`.
- Screenshot proof: `bundle://proof/SB04/browser/workflow-template-preview-dialog-large.png`.
- Corrective action during proof: initial canvas proof was too narrow and clipped nodes, so the stage inspector slot was removed and preview pan/zoom was set to 48% with full flow visible.
- Result: close enough for the requested UX intent. The user can inspect the workflow shape before adding it to drafts.

## Large-Screen Scope

- Validated at 1680x1000.
- Small and medium viewport screenshots were intentionally not captured because the user explicitly scoped this app to large screens only.
