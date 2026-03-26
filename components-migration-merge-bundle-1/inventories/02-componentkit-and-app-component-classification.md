# Component Classification Inventory

## `CanDoItAll.ComponentKit` Classification

### Promote To `CanDoItAll.Components.BaseLib`

Source root: `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\Components`

- `EmptyState.razor`
- `FilterBar.razor`
- `FormSection.razor`
- `HelpPopover.razor`
- `ListDetailShell.razor`
- `ListPanelHeader.razor`
- `LoadingState.razor`
- `PageHeader.razor`
- `PageScaffold.razor`
- `PageScaffoldMode.cs`
- `SecondaryTabs.razor`
- `SecondaryTabItem.cs`
- `SectionCard.razor`
- `SelectionListItem.razor`
- `StatusBadge.razor`
- `StickyActionFooter.razor`
- `SummaryTile.razor`
- `SummaryTiles.razor`

These are generic enough and are already used broadly across CanDoItAll modules.

### Promote To `CanDoItAll.Components.CanvasLib`

Source roots:

- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\Canvas`
- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\Components`
- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\wwwroot\js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\wwwroot\canvas-workbench.css`

Runtime component set:

- `AccessibilityMirrorLayer.razor`
- `CalendarCrudBridge.razor`
- `CalendarEventEditorModal.razor`
- `CalendarExportMenu.razor`
- `CalendarMiniMonthNavigator.razor`
- `CalendarSelectionPanel.razor`
- `CalendarTimeGridRenderer.razor`
- `CanvasCalendar.razor`
- `CanvasFloatingWindow.razor`
- `CanvasWorkbench.razor`
- `CanvasWorkbenchStage.razor`
- `ChipBadgePrimitive.razor`
- `ClipboardBridge.razor`
- `ConnectorAnchorOverlay.razor`
- `ConnectorPathPrimitive.razor`
- `ContainerPrimitive.razor`
- `ContextMenuHost.razor`
- `CreateActionPalette.razor`
- `DiagnosticsOverlay.razor`
- `DragDropController.razor`
- `EmptyStateOverlay.razor`
- `FloatingInspectorHost.razor`
- `GridBackdrop.razor`
- `GroupFrameOverlay.razor`
- `HitTestService.razor`
- `HoverFocusRouter.razor`
- `IconGlyphPrimitive.razor`
- `ImagePrimitive.razor`
- `InlineEditorComposer.razor`
- `KeyboardShortcutRouter.razor`
- `LayoutEngine.razor`
- `MarqueeSelectionOverlay.razor`
- `MinimapOverview.razor`
- `NodeCardComposer.razor`
- `SkeletonStateOverlay.razor`
- `SnapGuideSystem.razor`
- `TextBlockPrimitive.razor`
- `TooltipPopoverHost.razor`
- `TransformHandlesOverlay.razor`

### Move To `CanDoItAll.Components.Sandbox` Only

Source root: `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\Components`

- `AnimationTimelinePreview.razor`
- `CanvasBoundaryPreviewCard.razor`
- `CanvasSceneHostPreview.razor`
- `CommandHistoryStorePreview.razor`
- `InvalidationSchedulerPreview.razor`
- `JsInteropBridgePreview.razor`
- `LayerStackPreview.razor`
- `SceneNodeModelPreview.razor`
- `SerializationPersistencePackPreview.razor`
- `TextMeasureServicePreview.razor`
- `TunableComponentBoundary.razor`
- `TuningBoundaryRequest.cs`
- `ViewportControllerPreview.razor`

These are not reusable runtime components. They are catalog, tuning, or demo assets.

### Keep In `CanDoItAll.Components`

Source root: `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\Components`

- `AppShell.razor`
- `AppShellMode.cs`
- `AppTabStrip.razor`

These are CanDoItAll workbench compositions tied to CanDoItAll navigation, tabs, and shared-kernel concepts.

## `Zyphonote\App.Blazor\Components` Classification

Source root: `C:\repositories\Zyphonote\src\App.Blazor\Components`

### Promote To `CanDoItAll.Components.BaseLib` In Wave 1

These have high reuse potential and low domain coupling after neutral naming/styling cleanup:

- `Badge.razor`
- `BadgesGroup.razor`
- `Callout.razor`
- `Divider.razor`
- `EmptyState.razor`
- `Eyebrow.razor`
- `FactTable.razor`
- `FormRow.razor`
- `FormStack.razor`
- `InlineActions.razor`
- `ListGroup.razor`
- `ListItem.razor`
- `MetaList.razor`
- `MonoText.razor`
- `MutedInline.razor`
- `PageHeader.razor`
- `PageHeaderActions.razor`
- `PageShell.razor`
- `PanelCard.razor`
- `Pill.razor`
- `PillList.razor`
- `PlainList.razor`
- `SectionHead.razor`
- `SectionHeading.razor`
- `SmallText.razor`
- `StatusChip.razor`
- `Toolbar.razor`
- `ToolbarActions.razor`
- `ToolbarFields.razor`
- `ToolbarRow.razor`
- `WorkspacePanel.razor`
- `WorkspaceSplit.razor`

### Keep In `Zyphonote.Components` First, Re-Evaluate Later

These are reusable-looking, but currently too branded, too page-themed, or too entangled with `zy-sheet-*` CSS to force into the first shared wave:

- `ActionCard.razor`
- `AuthCard.razor`
- `Avatar.razor`
- `CardActions.razor`
- `CardGrid.razor`
- `HeroCard.razor`
- `PriceBar.razor`
- `PriceRow.razor`
- `ProfileField.razor`
- `ProfileTagChip.razor`
- `ProfileTagChipRow.razor`
- `ProfileToggle.razor`
- `SettingsSwitchLabel.razor`
- `SettingsSwitchRow.razor`
- `SheetCard.razor`
- `SheetCardHeading.razor`
- `SheetCardTop.razor`
- `SheetField.razor`
- `SheetGrid.razor`
- `SheetNote.razor`
- `SheetSection.razor`
- `ZyWorkspaceModal.razor`

### Keep In `Zyphonote.Components` Permanently Unless A Separate Shared Need Appears

These are domain- or product-specific:

- `BoughtLibraryCardsList.razor`
- `BuilderStatBox.razor`
- `BuilderStatStrip.razor`
- `CardStatsWithNumber.razor`
- `CatalogCardPreview.razor`
- `Chip.razor`
- `ChipRow.razor`
- `ChordInput.razor`
- `CreatorAvatar.razor`
- `CreatorLine.razor`
- `CreatorSocialLink.razor`
- `DashboardActions.razor`
- `DebugToggle.razor`
- `ImmersiveRibbonTabs.razor`
- `IntervalInput.razor`
- `KeyboardKeySvg.razor`
- `KeyboardOctaveSvg.razor`
- `KeyboardSvg.razor`
- `LeadSheetSvg.razor`
- `LearningBuilderPackageCardsList.razor`
- `LearningPackageStudyHeaderCards.razor`
- `LearningPackageStudySidebarCards.razor`
- `LegalToc.razor`
- `LegalTocNav.razor`
- `MarketplaceListingsGrid.razor`
- `MidiInputKeyboard.razor`
- `MidiLiveInputStatus.razor`
- `NotationEditor.razor`
- `NoteInput.razor`
- `OwnedScoreCardsList.razor`
- `OwnedScorePickerModal.razor`
- `PlaylistOverviewCardsList.razor`
- `QuickChordInput.razor`
- `QuickIntervalInput.razor`
- `QuickNoteInput.razor`
- `RepositoryGraphCanvas.razor`
- `ResultPanel.razor`
- `ScoreCreationWizard.razor`
- `ScoreRepositoryWorkbench.razor`
- `ScoreWorkbenchBranchRow.razor`
- `ScoreWorkbenchField.razor`
- `ScoreWorkbenchForm.razor`
- `ScoreWorkbenchGrid.razor`
- `ScoreWorkbenchItem.razor`
- `ScoreWorkbenchItemTop.razor`
- `ScoreWorkbenchList.razor`
- `StaffClefSvg.razor`
- `StaffSvg.razor`
- `StatBox.razor`
- `StatsCardRow.razor`
- `StatsGrid.razor`
- `TagTextEdit.razor`
- `ZyNotificationHost.razor`

## Important Classification Rule

If a component promotion would require pulling large chunks of `zyphonote-compat.css`, stop and keep it inside `Zyphonote.Components` for that wave. The shared layer must be cleaner than the current app layer, not a renamed copy of it.
