# Dependency Map

This map is intentionally practical rather than mathematically exhaustive. It shows the main prerequisite chain a Codex agent should respect.

## Wave 1

| Component | Primary dependencies | Category | Scope |
| --- | --- | --- | --- |
| CanvasSceneHost | JsInteropBridge, CanvasThemeTokenPack, DiagnosticsOverlay | Utility and infrastructure components | shared |
| CanvasThemeTokenPack | CanvasSceneHost | Utility and infrastructure components | shared |
| CommandHistoryStore | SerializationPersistencePack | Utility and infrastructure components | shared |
| DragDropController | HitTestService, SelectionModel, SnapGuideSystem, InvalidationScheduler | Interactive components | shared |
| GridBackdrop | CanvasSceneHost, ViewportController, SnapGuideSystem | Advanced graphical components | shared |
| HitTestService | SceneNodeModel, LayerStack, ViewportController | Interactive components | shared |
| HoverFocusRouter | SelectionModel, TooltipPopoverHost, AccessibilityMirrorLayer | Interactive components | shared |
| InvalidationScheduler | SceneNodeModel, LayerStack, AnimationTimeline | Utility and infrastructure components | shared |
| JsInteropBridge | CanvasSceneHost | Utility and infrastructure components | shared |
| KeyboardShortcutRouter | SelectionModel, HoverFocusRouter, CommandHistoryStore | Interactive components | shared |
| LayerStack | CanvasSceneHost, SceneNodeModel, InvalidationScheduler | Utility and infrastructure components | shared |
| LayoutEngine | TextMeasureService, SceneNodeModel, ViewportController | Layout and navigation components | shared |
| SceneNodeModel | CanvasSceneHost, LayerStack, InvalidationScheduler | Utility and infrastructure components | shared |
| SelectionModel | HitTestService, MarqueeSelectionOverlay, HoverFocusRouter | Selection and transform components | shared |
| SerializationPersistencePack | SceneNodeModel, CommandHistoryStore | Utility and infrastructure components | shared |
| TextMeasureService | JsInteropBridge, CanvasThemeTokenPack | Text components | shared |
| ViewportController | CanvasSceneHost, InvalidationScheduler, GridBackdrop | Layout and navigation components | shared |

## Wave 2

| Component | Primary dependencies | Category | Scope |
| --- | --- | --- | --- |
| CanvasWorkbenchShell | CanvasSceneHost, ViewportController, SelectionModel, ContextMenuHost, CreateActionPalette | Layout and navigation components | shared |
| CanvasWorkbenchStageShell | CanvasWorkbenchShell, CanvasThemeTokenPack | Layout and navigation components | shared |
| ChipBadgePrimitive | TextBlockPrimitive, IconGlyphPrimitive, CanvasThemeTokenPack | Basic primitives | shared |
| ConnectorPathPrimitive | SceneNodeModel, ConnectorAnchorOverlay, CanvasThemeTokenPack | Connector and relationship components | shared |
| ContainerPrimitive | CanvasThemeTokenPack, TextBlockPrimitive, IconGlyphPrimitive | Containers | shared |
| ContextMenuHost | HoverFocusRouter, KeyboardShortcutRouter, TextBlockPrimitive, IconGlyphPrimitive | Overlay, inspector, and helper components | shared |
| CreateActionPalette | ContextMenuHost, InlineEditorComposer, TextBlockPrimitive, IconGlyphPrimitive | Editing components | shared |
| FloatingInspectorHost | CanvasWorkbenchStageShell, HoverFocusRouter, CanvasThemeTokenPack | Overlay, inspector, and helper components | shared |
| GroupFrameOverlay | LayoutEngine, SelectionModel, ContainerPrimitive | Overlay, inspector, and helper components | shared |
| IconGlyphPrimitive | CanvasThemeTokenPack, TextBlockPrimitive | Basic primitives | shared |
| ImagePrimitive | ContainerPrimitive, CanvasThemeTokenPack, TextBlockPrimitive | Image components | shared |
| InlineEditorComposer | HoverFocusRouter, KeyboardShortcutRouter, TextBlockPrimitive, ContainerPrimitive | Editing components | shared |
| NodeCardComposer | ContainerPrimitive, TextBlockPrimitive, ChipBadgePrimitive, ImagePrimitive, IconGlyphPrimitive | Containers | shared |
| TextBlockPrimitive | TextMeasureService, CanvasThemeTokenPack | Text components | shared |

## Wave 3

| Component | Primary dependencies | Category | Scope |
| --- | --- | --- | --- |
| ProjectStructureActionCatalogAdapter | CreateActionPalette, ContextMenuHost | Project Structure domain components | domain-specific |
| ProjectStructureGraphAdapter | CanvasWorkbenchShell, NodeCardComposer, ProjectStructureActionCatalogAdapter | Project Structure domain components | domain-specific |
| ProjectStructurePlacementPolicy | LayoutEngine, ViewportController, SnapGuideSystem | Project Structure domain components | domain-specific |

## Wave 4

| Component | Primary dependencies | Category | Scope |
| --- | --- | --- | --- |
| PromptFactoryCatalogToolbox | CreateActionPalette, ContextMenuHost | Prompt Factory domain components | domain-specific |
| PromptFactorySessionGraphAdapter | CanvasWorkbenchShell, NodeCardComposer, PromptFactoryCatalogToolbox, PromptRunBranchLane, PromptSessionAttachmentNode | Prompt Factory domain components | domain-specific |
| PromptFactoryUndoRedoAdapter | CommandHistoryStore, KeyboardShortcutRouter, SerializationPersistencePack | Prompt Factory domain components | domain-specific |
| PromptRunBranchLane | LayoutEngine, GroupFrameOverlay, TextBlockPrimitive | Prompt Factory domain components | domain-specific |
| PromptSessionAttachmentNode | NodeCardComposer, ImagePrimitive, ChipBadgePrimitive, TooltipPopoverHost | Prompt Factory domain components | domain-specific |

## Wave 5

| Component | Primary dependencies | Category | Scope |
| --- | --- | --- | --- |
| CalendarCrudBridge | JsInteropBridge, SerializationPersistencePack | Calendar domain components | shared |
| CalendarEventEditorModal | CalendarCrudBridge, TextBlockPrimitive, ContainerPrimitive | Calendar domain components | shared |
| CalendarExportMenu | CanvasCalendarHost, ContextMenuHost, SerializationPersistencePack | Calendar domain components | shared |
| CalendarMiniMonthNavigator | CanvasCalendarHost, CalendarSelectionPanel, CanvasThemeTokenPack | Calendar domain components | shared |
| CalendarSelectionPanel | CanvasCalendarHost, TextBlockPrimitive, ChipBadgePrimitive | Calendar domain components | shared |
| CalendarTimeGridRenderer | CanvasCalendarHost, TextMeasureService, CanvasThemeTokenPack | Calendar domain components | shared |
| CanvasCalendarHost | CanvasSceneHost, CalendarCrudBridge, CalendarSelectionPanel | Calendar domain components | shared |
| ProjectCalendarAdapter | CanvasCalendarHost, SerializationPersistencePack, CalendarCrudBridge | Calendar domain components | domain-specific |
| ProjectCalendarStateParser | SerializationPersistencePack, ProjectCalendarAdapter | Calendar domain components | domain-specific |

## Wave 6

| Component | Primary dependencies | Category | Scope |
| --- | --- | --- | --- |
| AccessibilityMirrorLayer | CanvasSceneHost, SelectionModel, HoverFocusRouter, SerializationPersistencePack | Utility and infrastructure components | shared |
| AnimationTimeline | InvalidationScheduler, ViewportController, CanvasThemeTokenPack | Advanced graphical components | shared |
| ClipboardBridge | SelectionModel, SerializationPersistencePack, KeyboardShortcutRouter, CommandHistoryStore | Editing components | shared |
| ConnectorAnchorOverlay | ConnectorPathPrimitive, HitTestService, HoverFocusRouter, DiagnosticsOverlay | Connector and relationship components | shared |
| DiagnosticsOverlay | CanvasSceneHost, LayerStack, InvalidationScheduler | Diagnostic and developer components | shared |
| EmptyStateOverlay | CanvasWorkbenchStageShell, CreateActionPalette, TextBlockPrimitive, IconGlyphPrimitive | Overlay, inspector, and helper components | shared |
| MarqueeSelectionOverlay | SelectionModel, HitTestService, LayerStack | Selection and transform components | shared |
| MinimapOverview | ViewportController, SceneNodeModel, LayerStack, GridBackdrop | Layout and navigation components | shared |
| ProjectStructureValidationOverlay | TooltipPopoverHost, DiagnosticsOverlay, ChipBadgePrimitive, ConnectorPathPrimitive | Project Structure domain components | domain-specific |
| RecommendationOverlay | TooltipPopoverHost, ChipBadgePrimitive, HoverFocusRouter, SelectionModel | Prompt Factory domain components | domain-specific |
| SkeletonStateOverlay | CanvasWorkbenchStageShell, ContainerPrimitive, AnimationTimeline | Overlay, inspector, and helper components | shared |
| SnapGuideSystem | GridBackdrop, SelectionModel, DragDropController, ViewportController | Selection and transform components | shared |
| TooltipPopoverHost | HoverFocusRouter, CanvasThemeTokenPack, TextBlockPrimitive | Overlay, inspector, and helper components | shared |
| TransformHandlesOverlay | SelectionModel, HitTestService, DragDropController, ConnectorAnchorOverlay | Selection and transform components | shared |
