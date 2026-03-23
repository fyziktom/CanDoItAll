# Future-Feature Simulation

## Method

Four realistic future features were simulated as a check on bundle completeness: two for Project Structure and two for Prompt Factory. The question for each simulation was:

- Can a UI developer implement this using only the proposed components and integration rules?
- Is any low-level primitive still missing?
- Is any interaction pattern still missing?
- Is any domain adapter or validation guidance still missing?

## Simulated features

## Project Structure — dependency authoring with anchor handles and alignment guides

**Area:** Project Structure

**Feature goal:** Allow the user to create or reroute dependency connectors by dragging from anchor handles and snapping endpoints/alignment to nearby nodes or lanes.

**Required components**

- ProjectStructureGraphAdapter
- ConnectorAnchorOverlay
- ConnectorPathPrimitive
- HitTestService
- SelectionModel
- DragDropController
- SnapGuideSystem
- ProjectStructureValidationOverlay
- TooltipPopoverHost

**Simulation evaluation**

- The bundle provides every needed low-level primitive and interaction pattern once ConnectorAnchorOverlay, HitTestService, and SnapGuideSystem are implemented.
- The domain-specific validation overlay covers dependency-quality feedback that the base framework should not hardcode.
- No additional page-level infrastructure is required beyond the adapter and service integration already specified.

**Result:** Pass

## Project Structure — milestone grouping frame with inline toolbar, minimap, and export snapshot

**Area:** Project Structure

**Feature goal:** Let users create a milestone frame that groups nodes, exposes inline actions, appears in a minimap, and can be exported as a structured status snapshot.

**Required components**

- GroupFrameOverlay
- ContainerPrimitive
- InlineEditorComposer
- MinimapOverview
- SerializationPersistencePack
- ProjectStructurePlacementPolicy
- ProjectStructureValidationOverlay
- CanvasWorkbenchShell

**Simulation evaluation**

- The bundle contains the required grouping, overlay, minimap, and serialization building blocks.
- Inline toolbar needs only NodeCardComposer slot support, already captured in the advanced version of NodeCardComposer and ContainerPrimitive.
- Export snapshot uses the same persistence pack and adapter boundaries already defined.

**Result:** Pass

## Prompt Factory — in-canvas recommendation overlay for suggested blocks and missing inputs

**Area:** Prompt Factory

**Feature goal:** Show recommendation badges and rich popovers near selected nodes, with accept/dismiss actions that can insert suggested blocks into the session graph.

**Required components**

- PromptFactorySessionGraphAdapter
- PromptFactoryCatalogToolbox
- RecommendationOverlay
- TooltipPopoverHost
- ChipBadgePrimitive
- SelectionModel
- CreateActionPalette
- PromptFactoryUndoRedoAdapter

**Simulation evaluation**

- The bundle now includes RecommendationOverlay explicitly because the simulation identified it as a likely near-term requirement.
- TooltipPopoverHost and CreateActionPalette provide the missing UI mechanics that the current codebase does not yet isolate.
- Undo/redo integration is covered through PromptFactoryUndoRedoAdapter plus CommandHistoryStore.

**Result:** Pass

## Prompt Factory — duplicate and reorder selected subgraph with clipboard and undo/redo

**Area:** Prompt Factory

**Feature goal:** Allow users to copy or duplicate a selected prompt subgraph, paste it near the current viewport, reorder branch lanes, and undo the whole operation cleanly.

**Required components**

- ClipboardBridge
- CommandHistoryStore
- PromptFactoryUndoRedoAdapter
- PromptRunBranchLane
- DragDropController
- SelectionModel
- SerializationPersistencePack
- KeyboardShortcutRouter

**Simulation evaluation**

- ClipboardBridge and CommandHistoryStore are the critical enabling components; both are now part of the bundle.
- Branch-lane reordering is supported by the PromptRunBranchLane component and shared drag/snap infrastructure.
- No missing low-level primitive remains after adding clipboard, history, and keyboard routing to the shared inventory.

**Result:** Pass

## Completeness outcome

The simulation originally exposed the need to make the following components explicit in the bundle:

- ConnectorAnchorOverlay
- ClipboardBridge
- TooltipPopoverHost
- RecommendationOverlay
- ProjectCalendarStateParser

Those components are now part of the inventory and have full implementation folders.

## Final simulation verdict

**Pass.** After adding the missing explicit components above, the simulated future-feature set can be implemented without creating a new parallel framework or reintroducing page-level hacks.
