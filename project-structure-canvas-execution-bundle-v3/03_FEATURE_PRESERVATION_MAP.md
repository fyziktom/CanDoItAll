# Feature preservation map

Codex must treat the following features as **non-negotiable preservation scope**.

If a task risks any item in this map, that task is **not done** until the feature is restored and revalidated.

| ID | Category | Feature | Existing coverage / notes |
| --- | --- | --- | --- |
| F01 | Workbench chrome | Toolbar toggles for Blocks, Health, and Inspector/Selection surfaces | Renders_selection_window_and_toolbar_toggles_without_stage_inspector_column; Health_window_toggle_restores_the_default_offset_that_keeps_the_toolbox_clear; Project_structure_feedback_fixes_are_validated_in_browser |
| F02 | Floating windows | Toolbox floating window open/minimize/hide/drag/restore | Expanded_window_renders_icon_only_actions_with_accessible_labels; Minimized_window_renders_expand_and_hide_icons_without_text_labels; Project_structure_feedback_fixes_are_validated_in_browser |
| F03 | Toolbox | Toolbox search filters standard block actions | Project_structure_feedback_fixes_are_validated_in_browser |
| F04 | Toolbox | Toolbox accordion group expand/collapse | Project_structure_feedback_fixes_are_validated_in_browser |
| F05 | Create actions | Quick create and grouped create actions from toolbox/context menu/selection flows | Prompt_flow_context_actions_include_wizard_and_create_tools; Group_context_actions_expose_border_and_shared_status_tools; Markdown_create_definition_keeps_text_fields_and_file_upload_enabled; Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs |
| F06 | Health window | Health floating window with counts, spotlight, and validate-selected action | Health_window_toggle_restores_the_default_offset_that_keeps_the_toolbox_clear; Blocked_validation_nodes_surface_annotations; Summary_counts_blocked_review_and_priority_nodes |
| F07 | Selection window | Selection window empty state | Renders_selection_window_and_toolbar_toggles_without_stage_inspector_column |
| F08 | Selection window | Multi-select common actions, batch status/progress/marker/priority, focus-first, clear-selection | Persisted_multi_select_state_renders_common_actions_in_selection_window |
| F09 | Selection window | Single-select detail view with badges, quick signals, node actions, and advanced details | Selected_nodes_render_advanced_details_and_keep_delete_last_in_action_order; File_selection_panel_uses_semantic_badges_and_suppresses_duplicate_type_metadata |
| F10 | Linking | Link mode, reconnect, and disconnect workflows | Project_nodes_use_project_specific_actions_instead_of_generic_graph_mutations; Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs |
| F11 | Attachments | Inline managed attachment preview in selection panel | Pdf_attachment_nodes_render_inline_preview_and_open_modal_without_navigation; Audio_attachment_nodes_render_audio_preview_and_local_open_action_when_host_supports_it |
| F12 | Attachments | Attachment preview modal for PDF/video/audio/document | Pdf_attachment_nodes_render_inline_preview_and_open_modal_without_navigation; Double_clicking_pdf_attachment_nodes_keeps_preview_modal_behavior |
| F13 | Quick actions | Quick action dialog on double-click/open with node-specific actions | Double_clicking_prompt_flow_nodes_opens_quick_action_modal_and_wizard_new_tab_action; Double_clicking_launchable_runtime_nodes_opens_quick_action_modal_and_runs_powershell |
| F14 | Navigation | Prompt flow nodes expose wizard navigation in inspector and quick action modal | Prompt_flow_nodes_expose_wizard_navigation_from_the_inspector; Double_clicking_prompt_flow_nodes_opens_quick_action_modal_and_wizard_new_tab_action |
| F15 | Navigation | Project/subproject/shared-parent nodes open related structure in a new tab | Double_clicking_project_subproject_nodes_opens_related_structure_in_new_tab; Double_clicking_shared_parent_project_nodes_opens_related_structure_in_new_tab |
| F16 | Hierarchy | Add subproject dialog from selected root project | Selected_root_project_can_add_subproject_from_the_selection_panel |
| F17 | Hierarchy | Reconnect parent dialog from selected subproject | Selected_subproject_can_reconnect_parent_from_the_selection_panel |
| F18 | Summary | Summary modal with inline status edits | Selected_nodes_with_children_open_summary_modal_and_show_export_actions |
| F19 | Exports | Export summary workbook (XLSX/CSV-backed behavior) | Selected_nodes_with_children_open_summary_modal_and_show_export_actions; Project_structure_artifacts_capture_required_canvas_evidence |
| F20 | Exports | Export Gantt from summary | Selected_nodes_with_children_open_summary_modal_and_show_export_actions; Project_structure_artifacts_capture_required_canvas_evidence |
| F21 | Exports | Export image from selected hierarchy / mindmap capture | Project_structure_export_image_capture_generates_i18_artifacts |
| F22 | Transcript | Transcript provider confirmation dialog and transcript actions | Transcript_nodes_open_confirmation_dialog_with_provider_selection; Project_structure_artifacts_capture_required_canvas_evidence |
| F23 | Mermaid | Mermaid viewer modal with diagram type detection | Selected_mermaid_nodes_open_viewer_modal_with_detected_diagram_type |
| F24 | Deletion | Delete confirmation flow | Selected_nodes_render_advanced_details_and_keep_delete_last_in_action_order |
| F25 | Runtime launch | Runtime launch actions (PowerShell/admin) and launch feedback | Launchable_runtime_nodes_render_powershell_actions_and_surface_launch_feedback; Double_clicking_launchable_runtime_nodes_opens_quick_action_modal_and_runs_powershell; Non_launchable_nodes_do_not_render_runtime_launch_actions |
| F26 | Outline/support | Outline list selection sync below the canvas | Renders_shared_structure_workbench_and_updates_inspector_from_outline_selection |
| F27 | Health/support | Graph health support card below the canvas | Project_structure_artifacts_capture_required_canvas_evidence |
| F28 | Borders | Create selection border and clear selection borders | Group_context_actions_expose_border_and_shared_status_tools |
| F29 | Borders | Adopt moved nodes into borders after drag |  |
| F30 | State | Persisted selection, zoom/pan view state, and window state restoration | Persisted_multi_select_state_renders_common_actions_in_selection_window; Health_window_toggle_restores_the_default_offset_that_keeps_the_toolbox_clear |
| F31 | Rendering | Inline note creation and editing from the canvas | Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs; Edit_actions_open_prefilled_canvas_composer_for_supported_nodes; Edit_create_actions_update_existing_nodes_and_refresh_selection_panel |
| F32 | Media / file nodes | Compact path and subtype-specific mapping for repository/file nodes | File_backed_nodes_map_compact_path_payload_with_promoted_file_name; Repository_nodes_strip_full_path_from_lead_text_when_compact_path_is_present; File_nodes_use_subtype_specific_palettes |
| F33 | Shared surface | PromptFactory still works after shared CanvasWorkbench/CanvasFloatingWindow changes | Prompt_factory_canvas_surface_loads_and_exposes_shared_chrome; Prompt_library_catalog_is_exhaustively_available_from_prompt_gallery_and_factory_canvas; Prompt_factory_artifacts_capture_toolbox_preview_and_single_add_flow |
| F34 | Shared surface | Sandbox canvas page still works after shared canvas changes | Workbench_renders_toolbar_hint_and_help_overlay; Workbench_uses_settings_icon_and_marks_settings_overlay_with_toolbar_safe_modifier |
| F35 | Toolbox UX | Toolbox rows are single-line, compact, keyboard-accessible, and show description on hover tooltip | No direct current browser test; must add Playwright and component coverage |
| F36 | Overlay isolation | Scrolling and clicking inside toolbox and floating windows never leak into canvas pan/zoom/drag handlers | Partial toolbox scroll smoke only; no direct expand/collapse + wheel isolation browser proof |
| F37 | Renderer | Dense scene layers render on actual HTML5 canvas instead of DOM/SVG for runtime workbench surfaces | CanvasBenchmark page exists but does not validate runtime adoption |
| F38 | Renderer parity | Node card behaviors survive canvas migration, including selection, drag, double-open, collapse, and compact-path copy | Existing ProjectStructure interaction tests cover parts of the behavior but not canvas hot-zones |
| F39 | Assets | CanvasLib asset loading is centralized and deterministic without duplicated script lists in app entrypoints | No direct test coverage |
| F40 | Benchmark | CanvasBenchmark sandbox page remains usable and becomes the renderer migration evidence harness | No dedicated automated benchmark regression gate yet |

## Additional preservation rules

### Shared-canvas changes must preserve both primary consumers
- `ProjectStructurePage`
- `PromptFactoryPage`

### Preview-boundary components are not runtime renderer code, but they still must survive
PromptFactory support surfaces and sandbox demos rely on several boundary preview components.  
They may be relocated and clearly labeled, but they must not be silently deleted.

### Real-canvas migration must preserve node-level behaviors
The new renderer must preserve at least these runtime card interactions:
- select node,
- multi-select,
- drag node(s),
- open node on double activation,
- toggle collapse,
- copy compact path,
- open context menu,
- create/edit note through overlays,
- open preview flows and quick action flows.

### Selection and state restoration remain required
Even after moving the scene to canvas:
- selection must persist when product rules require it,
- zoom/pan and window state restoration must remain available,
- collapsed state and group frames must still round-trip.

### Export and accessibility are part of parity
A renderer migration is **not** complete if:
- image export breaks,
- accessibility mirror becomes stale or misleading,
- keyboard and screen-reader support regress materially.

## Recommended preservation order during implementation

1. Lock tests and screenshots for toolbox, selection window, context menu, export, and PromptFactory.
2. Preserve the public `CanvasWorkbench` parameter/event contract while changing internals.
3. Keep a staged rollout or fallback path until feature parity is proven.
4. Remove legacy paths only after full validation is green.
