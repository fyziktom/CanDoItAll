# Task sequence

## P0
1. `tasks/P0-01_overlay_input_isolation_and_wheel_ownership.md`
2. `tasks/P0-02_commit_only_floating_window_persistence.md`
3. `tasks/P0-03_commit_only_canvas_state_persistence_and_ui_state_ownership_cleanup.md`
4. `tasks/P0-04_batch_node_move_persistence.md`
5. `tasks/P0-05_avoid_full_surface_reloads_after_simple_mutations.md`
6. `tasks/P0-06_runtime_surface_cleanup_and_support_demo_separation.md`
7. `tasks/P0-07_instrumentation_and_browser_gates_foundation.md`

## P1
1. `tasks/P1-01_retained_dom_svg_renderer_for_nodes,_links,_and_frames.md`
2. `tasks/P1-02_viewport_culling_and_filtered_scene_projection.md`
3. `tasks/P1-03_dirty_region_drag_loop_owned_by_js.md`
4. `tasks/P1-04_selection_panel_decomposition_and_lazy_expensive_support_surfaces.md`

## P2
1. `tasks/P2-01_scene_patch_protocol_and_plain_js_modularization.md`
2. `tasks/P2-02_dedicated_screenshot_and_performance_regression_suite.md`

## P3
1. `tasks/P3-01_optional_true_canvas_renderer_spike.md`
2. `tasks/P3-02_optional_shared_library_consolidation.md`

## Ordering rule

Do not advance to the next task until the current task passes:
- impacted component tests,
- impacted browser/screenshot tests,
- impacted performance gates.

## Suggested execution style

Prefer one task per commit or one tightly related pair only when the second task is trivial and validation remains clear.
