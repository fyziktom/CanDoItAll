# Current state

## Current strengths

### Composition and module registration
`ProcessesModuleServiceCollectionExtensions.cs` follows the solution’s composition style and registers the module cleanly.

### Cross-module seam direction
`ProjectPartyIntegrationContracts.cs` shows the right kind of bridge-based direction for cross-module collaboration. This is preferable to tighter compile-time coupling.

### Existing regression surface
The module already has meaningful coverage across:
- integration flows,
- workspace/canvas component behavior,
- MCP tool projections,
- import/export/template handling.

### Some good transaction practice already exists
`DeleteAsync` in `ProcessesService.Publication.cs` already demonstrates explicit transaction usage and bulk deletion patterns that should influence the rest of the mutation core.

## Current weaknesses

### Dependency shape drift
Dependency semantics still rely on both legacy fields and explicit dependency rows.

### Mutation purity drift
Validation and normalization are not clearly separated.

### Save pipeline fragility
Definition child graphs are rewritten destructively.

### Publish/runtime conflict fragility
There are race windows around slug, version, and concurrent mutation paths.

### Oversized orchestration points
The service façade and workspace surface are already under strain.

### Query breadth
Some query surfaces still load more than they need and aggregate in memory.

## Important observed test anchors

### Integration
- `StartRunAsync_prefills_project_role_binding_and_persists_runtime_signals`
- `SeedBaselineAsync_supports_global_then_project_scoped_baselines_without_slug_collisions`
- `ListDefinitionsAsync_counts_roles_and_steps_from_the_current_summary_version_only`
- `PublishAsync_rejects_unused_branch_outcomes`
- `TransitionStepAsync_routes_selected_branch_and_skips_the_non_selected_path`
- `PublishAsync_preserves_role_and_branch_canvas_positions_in_the_next_draft`
- `GetEditorAsync_and_publish_clone_preserve_artifact_input_links`
- `SaveAsync_rejects_artifact_inputs_without_matching_structural_dependencies`
- `TransitionStepAsync_waits_for_all_dependencies_before_join_step_becomes_ready`

### Components
- `Global_workspace_loads_persisted_definitions_on_the_first_render_without_query_parameters`
- `Steps_canvas_connection_actions_create_and_delete_branch_and_step_dependencies`
- `Steps_canvas_connection_actions_create_and_delete_role_participation_and_artifact_links_and_persist`
- `Steps_canvas_node_moves_update_role_and_branch_positions_in_editor_state`
- `Steps_canvas_node_moves_coalesce_rapid_updates_into_one_persisted_definition_update`
- `Templates_dialog_adds_role_templates_into_the_current_definition_without_closing_the_modal`
- `Templates_dialog_adds_artifact_templates_into_the_selected_definition_step_without_closing_the_modal`

### MCP
- `ProcessesDefinitionSaveAsync_returns_successful_structured_content`
- `ProcessesDefinitionPublishAsync_returns_structured_validation_failure`
- `ProcessesStepTransitionAsync_forwards_selected_branch_outcome`
- `ProcessesTemplatesListAsync_returns_catalog_entries`
- `ProcessesTemplateImportAsync_returns_projected_import_result`

## Current-state conclusion

The module is not broken in a simplistic sense. It already works in several important paths. The problem is that core architectural risks are now concentrated in a few specific places, and those places are exactly the ones that future growth would stress the most.
