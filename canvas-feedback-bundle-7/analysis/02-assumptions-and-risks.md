# Assumptions And Risks

## Assumptions

- The safest path treatment is additive: keep legacy lead-text support for unaffected nodes, but introduce typed optional metadata for compact-path rendering on path-backed nodes.
- The non-preview double-click modal should be page-owned, because the secondary action has to reuse Workbench command knowledge rather than introduce a second JavaScript-only action matrix.
- The settings issue is shared-canvas chrome, so the correct fix lives in `CanvasWorkbench.razor` plus `canvas-workbench.css` instead of page-specific hacks.

## Risks

- Any change to `CanvasWorkbenchContracts.cs` can affect other canvas consumers if existing serialization and rendering paths are not kept backward compatible.
- Introducing a quick-action modal creates a risk of behavior drift if it duplicates command eligibility rules instead of consuming existing page and service logic.
- Some node types may not have a safe edit path. If that is true, the implementation must expose an explicit limitation in the modal and the raw-note closure rather than inventing a fake edit action.
- Clipboard affordance work can feel broken if the copied-state icon does not reset reliably or if hover-only full-path access is not keyboard reachable.

## Risk Handling

- Keep new node presentation fields optional and derived in C#, with existing title and lead-text behavior preserved for nodes that do not participate.
- Derive the quick-action modal from existing action and edit resolution instead of a separate stringly typed switch in JavaScript.
- Add focused browser proof for copy feedback, quick-action modal content, and toolbar-safe settings placement before closing the raw notes.
- If any node type remains intentionally non-editable, capture that explicitly in the execution report instead of silently broadening or narrowing the requirement.
