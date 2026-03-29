# Shared consumers and legacy plan

## PromptFactory is not optional

`PromptFactoryPage` is a real shared-canvas consumer:
- it uses `CanvasWorkbench`,
- it uses `CanvasFloatingWindow`,
- it also uses preview-boundary components in its support lane.

Any shared-canvas migration that only validates ProjectStructure is incomplete.

## PromptFactory-specific cautions

### Hot-path persistence
PromptFactory currently persists canvas UI state too eagerly:
- selection changed -> persist,
- nodes moved -> persist,
- state changed -> persist.

This should be aligned with the commit-only model introduced for ProjectStructure.

### Preview-boundary support lane
PromptFactory uses components such as:
- `NodeCardComposer`
- `ConnectorPathPrimitive`
- `GroupFrameOverlay`
- `ImagePrimitive`
- `InlineEditorComposer`

These are not runtime scene layers.  
They are preview/boundary documentation surfaces and should be kept, but relocated under a clear preview namespace/folder.

## Sandbox is the proof harness

The sandbox already contains:
- a shared `CanvasWorkbench` preview,
- a true-canvas prototype benchmark,
- other canvas-related surfaces.

Do not bypass it. Extend it and make it part of the evidence chain.

## Legacy ComponentKit plan

`CanDoItAll.ComponentKit` already says:
- it is legacy,
- active runtime is `CanDoItAll.Components.CanvasLib`,
- no new runtime consumers should be added.

This bundle recommends:
- keep ComponentKit clearly compatibility-only,
- do not mirror the full runtime refactor there,
- only touch it when a real compatibility need is proven.

## Legacy ProjectStructureCanvas plan

`ProjectStructureCanvas.razor` and `workbenchInterop.js` are useful reference material because they already implement real canvas rendering for an older module-specific path.

Recommended handling:
- use them as reference while building the new shared-canvas stage,
- do not route production runtime back to this old module-specific surface,
- archive or remove them only after the new shared runtime has full parity.

## Decision rule

If Codex encounters a choice between:
- preserving shared consumer safety,
- or performing a faster but risky cleanup,

choose shared consumer safety first.
