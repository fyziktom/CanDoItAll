# Feature preservation map

This file exists to prevent accidental regression.

Before Codex edits any task area, it must check the feature map and mark which features could be touched by the change.  
A task is not complete until all impacted features either:

- continue to pass their existing tests, or
- gain new tests and browser coverage where coverage was missing.

## How to use this map

1. Find the files you intend to touch.
2. Search this map for features whose source refs mention those files.
3. Build an impact list before editing.
4. After editing, rerun the matching validation gates.
5. If any feature fails, fix it before moving on.

## Must-not-lose rule

The refactor is allowed to change implementation, ownership, and internal architecture.  
It is **not** allowed to silently drop behavior.

The current workbench has a lot of product behavior hiding inside one page and one shared interop file. The only safe way to refactor it is to make the preservation list explicit.

## Feature inventory

### F01 — Toolbar toggles for Blocks, Health, and Inspector/Selection surfaces
- **Category:** Workbench chrome
- **Source refs:** ProjectStructurePage.razor:44-80; ProjectStructurePage.razor:514-803
- **Existing tests:** Renders_selection_window_and_toolbar_toggles_without_stage_inspector_column; Health_window_toggle_restores_the_default_offset_that_keeps_the_toolbox_clear; Project_structure_feedback_fixes_are_validated_in_browser
- **Relevant subbundles:** P0-01;P0-02;P0-06;P2-02
- **Validation:** bUnit toggle assertions; Playwright screenshot of default state and toggled windows

### F02 — Toolbox floating window open/minimize/hide/drag/restore
- **Category:** Floating windows
- **Source refs:** ProjectStructurePage.razor:81-175; ProjectStructurePage.ToolWindows.cs:24-47; CanvasFloatingWindow.razor; canvas-floating-window.js
- **Existing tests:** Expanded_window_renders_icon_only_actions_with_accessible_labels; Minimized_window_renders_expand_and_hide_icons_without_text_labels; Project_structure_feedback_fixes_are_validated_in_browser
- **Relevant subbundles:** P0-01;P0-02;P0-07;P2-02
- **Validation:** bUnit floating-window tests; Playwright drag and chrome screenshots

### F03 — Toolbox search filters standard block actions
- **Category:** Toolbox
- **Source refs:** ProjectStructurePage.ToolWindows.cs:49-114; ProjectStructurePage.razor:83-175
- **Existing tests:** Project_structure_feedback_fixes_are_validated_in_browser
- **Relevant subbundles:** P0-01;P0-06;P2-02
- **Validation:** Playwright search + scroll + screenshot

### F04 — Toolbox accordion group expand/collapse
- **Category:** Toolbox
- **Source refs:** ProjectStructurePage.ToolWindows.cs:49-100; ProjectStructurePage.razor:106-175
- **Existing tests:** Project_structure_feedback_fixes_are_validated_in_browser
- **Relevant subbundles:** P0-01;P2-02
- **Validation:** Playwright click accordion, verify open group count and no canvas event leakage

### F05 — Quick create and grouped create actions from toolbox/context menu/selection flows
- **Category:** Create actions
- **Source refs:** ProjectStructureCanvasCatalog.cs; ProjectStructureActionCatalogAdapter.cs; ProjectStructurePage.CreateCatalog.cs
- **Existing tests:** Prompt_flow_context_actions_include_wizard_and_create_tools; Group_context_actions_expose_border_and_shared_status_tools; Markdown_create_definition_keeps_text_fields_and_file_upload_enabled; Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs
- **Relevant subbundles:** P0-05;P1-04;P2-01
- **Validation:** bUnit catalog tests; Playwright create dialog and context create flows

### F06 — Health floating window with counts, spotlight, and validate-selected action
- **Category:** Health window
- **Source refs:** ProjectStructurePage.razor:176-252; ProjectStructurePage.razor:1796-1831
- **Existing tests:** Health_window_toggle_restores_the_default_offset_that_keeps_the_toolbox_clear; Blocked_validation_nodes_surface_annotations; Summary_counts_blocked_review_and_priority_nodes
- **Relevant subbundles:** P0-02;P0-06;P1-04
- **Validation:** bUnit window state restoration; screenshot with health window visible

### F07 — Selection window empty state
- **Category:** Selection window
- **Source refs:** ProjectStructurePage.razor:253-314
- **Existing tests:** Renders_selection_window_and_toolbar_toggles_without_stage_inspector_column
- **Relevant subbundles:** P1-04
- **Validation:** bUnit empty-state render

### F08 — Multi-select common actions, batch status/progress/marker/priority, focus-first, clear-selection
- **Category:** Selection window
- **Source refs:** ProjectStructurePage.razor:315-472; ProjectStructurePage.razor:1688-1751
- **Existing tests:** Persisted_multi_select_state_renders_common_actions_in_selection_window
- **Relevant subbundles:** P0-04;P0-05;P1-04
- **Validation:** bUnit multi-select panel; Playwright multi-select screenshot and batch action checks

### F09 — Single-select detail view with badges, quick signals, node actions, and advanced details
- **Category:** Selection window
- **Source refs:** ProjectStructurePage.razor:473-744; ProjectStructurePage.SelectionPanel.cs
- **Existing tests:** Selected_nodes_render_advanced_details_and_keep_delete_last_in_action_order; File_selection_panel_uses_semantic_badges_and_suppresses_duplicate_type_metadata
- **Relevant subbundles:** P1-04
- **Validation:** bUnit single-select detail tests; screenshot of file node and runtime node

### F10 — Link mode, reconnect, and disconnect workflows
- **Category:** Linking
- **Source refs:** ProjectStructurePage.razor:1295-1303; ProjectStructurePage.Workflows.cs:49-68; ProjectStructurePage.Workflows.cs:444-460
- **Existing tests:** Project_nodes_use_project_specific_actions_instead_of_generic_graph_mutations; Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs
- **Relevant subbundles:** P0-05;P1-03
- **Validation:** Targeted browser test for link mode and reconnect

### F11 — Inline managed attachment preview in selection panel
- **Category:** Attachments
- **Source refs:** ProjectStructurePage.razor:563-650; ProjectStructurePage.razor:2006-2139
- **Existing tests:** Pdf_attachment_nodes_render_inline_preview_and_open_modal_without_navigation; Audio_attachment_nodes_render_audio_preview_and_local_open_action_when_host_supports_it
- **Relevant subbundles:** P1-04
- **Validation:** bUnit preview render; screenshot of inline preview

### F12 — Attachment preview modal for PDF/video/audio/document
- **Category:** Attachments
- **Source refs:** ProjectStructurePage.razor:745-800; ProjectStructurePage.razor:2129-2139
- **Existing tests:** Pdf_attachment_nodes_render_inline_preview_and_open_modal_without_navigation; Double_clicking_pdf_attachment_nodes_keeps_preview_modal_behavior
- **Relevant subbundles:** P1-04;P2-02
- **Validation:** bUnit modal open/close; Playwright screenshot of modal

### F13 — Quick action dialog on double-click/open with node-specific actions
- **Category:** Quick actions
- **Source refs:** ProjectStructurePage.NodeQuickActions.cs; ProjectStructurePage.razor:1344-1355
- **Existing tests:** Double_clicking_prompt_flow_nodes_opens_quick_action_modal_and_wizard_new_tab_action; Double_clicking_launchable_runtime_nodes_opens_quick_action_modal_and_runs_powershell
- **Relevant subbundles:** P0-01;P1-04
- **Validation:** bUnit dialog tests; screenshot of quick action dialog

### F14 — Prompt flow nodes expose wizard navigation in inspector and quick action modal
- **Category:** Navigation
- **Source refs:** ProjectStructurePage.NodeEditing.cs; ProjectStructurePage.NodeQuickActions.cs
- **Existing tests:** Prompt_flow_nodes_expose_wizard_navigation_from_the_inspector; Double_clicking_prompt_flow_nodes_opens_quick_action_modal_and_wizard_new_tab_action
- **Relevant subbundles:** P1-04
- **Validation:** Existing bUnit tests

### F15 — Project/subproject/shared-parent nodes open related structure in a new tab
- **Category:** Navigation
- **Source refs:** ProjectStructurePage.ProjectHierarchy.cs:99-112; ProjectStructurePage.razor:1344-1355
- **Existing tests:** Double_clicking_project_subproject_nodes_opens_related_structure_in_new_tab; Double_clicking_shared_parent_project_nodes_opens_related_structure_in_new_tab
- **Relevant subbundles:** P1-04
- **Validation:** Existing bUnit tests

### F16 — Add subproject dialog from selected root project
- **Category:** Hierarchy
- **Source refs:** ProjectStructurePage.ProjectHierarchy.cs:19-171; ProjectStructurePage.razor:651-688
- **Existing tests:** Selected_root_project_can_add_subproject_from_the_selection_panel
- **Relevant subbundles:** P0-05;P1-04
- **Validation:** bUnit hierarchy dialog test

### F17 — Reconnect parent dialog from selected subproject
- **Category:** Hierarchy
- **Source refs:** ProjectStructurePage.ProjectHierarchy.cs:19-171; ProjectStructurePage.razor:651-688
- **Existing tests:** Selected_subproject_can_reconnect_parent_from_the_selection_panel
- **Relevant subbundles:** P0-05;P1-04
- **Validation:** bUnit hierarchy dialog test

### F18 — Summary modal with inline status edits
- **Category:** Summary
- **Source refs:** ProjectStructurePage.Workflows.cs:109-149; ProjectStructurePage.razor:893-980
- **Existing tests:** Selected_nodes_with_children_open_summary_modal_and_show_export_actions
- **Relevant subbundles:** P0-05;P1-04
- **Validation:** bUnit summary modal; screenshot of summary modal

### F19 — Export summary workbook (XLSX/CSV-backed behavior)
- **Category:** Exports
- **Source refs:** ProjectStructurePage.Workflows.cs:150-181; ProjectStructurePage.razor:907-909
- **Existing tests:** Selected_nodes_with_children_open_summary_modal_and_show_export_actions; Project_structure_artifacts_capture_required_canvas_evidence
- **Relevant subbundles:** P1-04;P2-02
- **Validation:** Browser export flow and artifact existence

### F20 — Export Gantt from summary
- **Category:** Exports
- **Source refs:** ProjectStructurePage.Workflows.cs:182-222; ProjectStructurePage.razor:907-909
- **Existing tests:** Selected_nodes_with_children_open_summary_modal_and_show_export_actions; Project_structure_artifacts_capture_required_canvas_evidence
- **Relevant subbundles:** P1-04;P2-02
- **Validation:** Browser export flow and artifact existence

### F21 — Export image from selected hierarchy / mindmap capture
- **Category:** Exports
- **Source refs:** ProjectStructurePage.Workflows.cs:223-271; CanvasWorkbench.razor:692-699; canvasWorkbenchInterop.js:5672-5685
- **Existing tests:** Project_structure_export_image_capture_generates_i18_artifacts
- **Relevant subbundles:** P1-01;P2-02;P3-01
- **Validation:** Playwright export image flow and artifacts

### F22 — Transcript provider confirmation dialog and transcript actions
- **Category:** Transcript
- **Source refs:** ProjectStructurePage.Workflows.cs:272-437; ProjectStructurePage.razor:981-1075
- **Existing tests:** Transcript_nodes_open_confirmation_dialog_with_provider_selection; Project_structure_artifacts_capture_required_canvas_evidence
- **Relevant subbundles:** P1-04;P2-02
- **Validation:** bUnit provider dialog; screenshot of provider confirmation

### F23 — Mermaid viewer modal with diagram type detection
- **Category:** Mermaid
- **Source refs:** ProjectStructurePage.Workflows.cs:438-443; ProjectStructurePage.razor:1077-1110
- **Existing tests:** Selected_mermaid_nodes_open_viewer_modal_with_detected_diagram_type
- **Relevant subbundles:** P1-04;P2-02
- **Validation:** bUnit mermaid modal; screenshot of viewer

### F24 — Delete confirmation flow
- **Category:** Deletion
- **Source refs:** ProjectStructurePage.Workflows.cs:69-106; ProjectStructurePage.razor:689-744
- **Existing tests:** Selected_nodes_render_advanced_details_and_keep_delete_last_in_action_order
- **Relevant subbundles:** P0-05;P1-04
- **Validation:** bUnit action order and dialog checks

### F25 — Runtime launch actions (PowerShell/admin) and launch feedback
- **Category:** Runtime launch
- **Source refs:** ProjectStructurePage.RuntimeLaunch.cs; ProjectStructurePage.razor:620-643
- **Existing tests:** Launchable_runtime_nodes_render_powershell_actions_and_surface_launch_feedback; Double_clicking_launchable_runtime_nodes_opens_quick_action_modal_and_runs_powershell; Non_launchable_nodes_do_not_render_runtime_launch_actions
- **Relevant subbundles:** P1-04
- **Validation:** Existing bUnit tests; runtime action screenshot

### F26 — Outline list selection sync below the canvas
- **Category:** Outline/support
- **Source refs:** ProjectStructurePage.razor:804-819
- **Existing tests:** Renders_shared_structure_workbench_and_updates_inspector_from_outline_selection
- **Relevant subbundles:** P0-06;P1-04
- **Validation:** bUnit outline selection test

### F27 — Graph health support card below the canvas
- **Category:** Health/support
- **Source refs:** ProjectStructurePage.razor:821-839
- **Existing tests:** Project_structure_artifacts_capture_required_canvas_evidence
- **Relevant subbundles:** P0-06
- **Validation:** Product decision plus screenshot if retained

### F28 — Create selection border and clear selection borders
- **Category:** Borders
- **Source refs:** ProjectStructurePage.razor:1737-1767; ProjectStructureGraphAdapter.cs:60-110
- **Existing tests:** Group_context_actions_expose_border_and_shared_status_tools
- **Relevant subbundles:** P0-05;P1-03
- **Validation:** Targeted unit tests for frame add/remove and screenshot with border

### F29 — Adopt moved nodes into borders after drag
- **Category:** Borders
- **Source refs:** ProjectStructurePage.Workflows.cs:462-543; ProjectStructurePage.razor:1310-1318
- **Existing tests:** None found; add coverage before risky changes.
- **Relevant subbundles:** P0-04;P1-03
- **Validation:** Add automated test before or during refactor

### F30 — Persisted selection, zoom/pan view state, and window state restoration
- **Category:** State
- **Source refs:** ProjectStructurePage.razor:1321-1859; CanvasWorkbench.razor:415-423; CanvasWorkbenchContracts.cs:123-202
- **Existing tests:** Persisted_multi_select_state_renders_common_actions_in_selection_window; Health_window_toggle_restores_the_default_offset_that_keeps_the_toolbox_clear
- **Relevant subbundles:** P0-02;P0-03
- **Validation:** bUnit state restoration; browser refresh persistence check

### F31 — Inline note creation and editing from the canvas
- **Category:** Rendering
- **Source refs:** ProjectStructurePage.razor:1381-1389; ProjectStructurePage.Workflows.cs; canvasWorkbenchInterop.js inline note behavior
- **Existing tests:** Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs; Edit_actions_open_prefilled_canvas_composer_for_supported_nodes; Edit_create_actions_update_existing_nodes_and_refresh_selection_panel
- **Relevant subbundles:** P0-05;P1-01;P1-03
- **Validation:** Browser note create/edit path and bUnit edit tests

### F32 — Compact path and subtype-specific mapping for repository/file nodes
- **Category:** Media / file nodes
- **Source refs:** ProjectStructureGraphAdapter.cs:229-241; ProjectStructurePage.razor:2062-2115
- **Existing tests:** File_backed_nodes_map_compact_path_payload_with_promoted_file_name; Repository_nodes_strip_full_path_from_lead_text_when_compact_path_is_present; File_nodes_use_subtype_specific_palettes
- **Relevant subbundles:** P1-01;P1-04
- **Validation:** Existing bUnit tests

### F33 — PromptFactory still works after shared CanvasWorkbench/CanvasFloatingWindow changes
- **Category:** Shared surface
- **Source refs:** PromptFactoryPage.razor:69-88; PromptFactoryPage.razor:166-284
- **Existing tests:** Prompt_factory_canvas_surface_loads_and_exposes_shared_chrome; Prompt_library_catalog_is_exhaustively_available_from_prompt_gallery_and_factory_canvas; Prompt_factory_artifacts_capture_toolbox_preview_and_single_add_flow
- **Relevant subbundles:** P0-01;P0-02;P2-01;P2-02
- **Validation:** Run PromptFactory browser suite after every shared-canvas change

### F34 — Sandbox canvas page still works after shared canvas changes
- **Category:** Shared surface
- **Source refs:** Components.Sandbox/Components/Pages/Canvas.razor
- **Existing tests:** Workbench_renders_toolbar_hint_and_help_overlay; Workbench_uses_settings_icon_and_marks_settings_overlay_with_toolbar_safe_modifier
- **Relevant subbundles:** P0-01;P2-01
- **Validation:** Smoke/manual validation or add a targeted browser test


## Coverage gaps that deserve attention early

The following areas either have weaker automated coverage or are risky enough that they should receive additional validation before deep refactoring:

- moved-node adoption into borders,
- overlay wheel ownership and overlay-to-scene event leakage,
- large-graph pan/drag performance counters,
- support/demo surface removal or relocation,
- cross-regression for PromptFactory and Sandbox after shared-canvas changes.

## Required practice for Codex

For every task:
- list impacted feature IDs in the implementation note or commit message,
- rerun matching tests,
- rerun matching browser screenshots,
- do not advance until the impacted features are green.
