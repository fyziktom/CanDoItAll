# Validation gates and screenshot scenarios

This file defines the **mandatory validation envelope** for the refactor.

A task that compiles but fails browser behavior, screenshots, or cross-surface regression is **not done**.

## Mandatory code/test gates

## Shared test commands to run locally

Use the smallest relevant subset first, then the broader suite before merging:

```bash
dotnet test CanDoItAll.slnx --filter "FullyQualifiedName~CanvasWorkbenchTests|FullyQualifiedName~CanvasFloatingWindowTests|FullyQualifiedName~ProjectStructure"
dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj
dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj
```

When shared-canvas code changes, also rerun PromptFactory coverage:

```bash
dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~Prompt_factory|FullyQualifiedName~Prompt_library"
```

If a task touches service or DB paths, rerun the relevant integration tests too.

## Existing high-value tests to keep green

### bUnit / component tests
- `Renders_selection_window_and_toolbar_toggles_without_stage_inspector_column`
- `Health_window_toggle_restores_the_default_offset_that_keeps_the_toolbox_clear`
- `Renders_shared_structure_workbench_and_updates_inspector_from_outline_selection`
- `Persisted_multi_select_state_renders_common_actions_in_selection_window`
- `Selected_nodes_with_children_open_summary_modal_and_show_export_actions`
- `Transcript_nodes_open_confirmation_dialog_with_provider_selection`
- `Pdf_attachment_nodes_render_inline_preview_and_open_modal_without_navigation`
- `Launchable_runtime_nodes_render_powershell_actions_and_surface_launch_feedback`
- `Workbench_renders_toolbar_hint_and_help_overlay`
- `Expanded_window_renders_icon_only_actions_with_accessible_labels`

### Existing browser tests already worth preserving/extending
- `Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs`
- `Project_structure_feedback_fixes_are_validated_in_browser`
- `Project_structure_feedback6_context_menu_is_validated_in_browser`
- `Project_structure_artifacts_capture_required_canvas_evidence`
- `Project_structure_feedback_7_is_validated_in_browser`
- `Project_structure_export_image_capture_generates_i18_artifacts`
- `Prompt_factory_canvas_surface_loads_and_exposes_shared_chrome`
- `Prompt_factory_artifacts_capture_toolbox_preview_and_single_add_flow`

## Required screenshot scenarios

At minimum, maintain screenshot evidence for these states:

1. initial ProjectStructure workbench shell,
2. toolbox window open and expanded,
3. toolbox search results,
4. toolbox internal scroll state,
5. health window visible,
6. single-select state for a representative node,
7. multi-select state with common actions visible,
8. quick action modal,
9. hierarchy dialog,
10. summary modal,
11. transcript provider confirmation dialog,
12. mermaid viewer,
13. attachment preview inline state,
14. attachment preview modal,
15. runtime launch action visibility,
16. export-image success state or resulting node,
17. large-graph viewport state (after the large fixture exists).

## Interaction assertions that must become browser gates

### Overlay isolation
- clicking toolbox headers must not trigger scene selection/pan,
- right-click inside toolbox must not open scene context menu,
- wheel inside toolbox/floating window must scroll content and must not zoom the scene.

### Window behavior
- toolbox window drag changes its position,
- final position persists only after commit,
- minimize/hide/restore still work.

### Scene behavior
- node context menu still works on real nodes,
- canvas background context menu still works on empty scene,
- selection, multi-select, drag, and pan still work on the scene.

## Large-graph validation

Once the deterministic large fixture exists:
- capture visible-node count and total-node count from debug counters,
- capture render counters before and after a pan session,
- capture drag counters before and after a multi-node drag,
- confirm culling and retained patching are active.

## Failure rule

If any required gate fails:
1. the current task remains open,
2. Codex must fix the failure,
3. the full impacted gate set must be rerun,
4. only then may the next task begin.
