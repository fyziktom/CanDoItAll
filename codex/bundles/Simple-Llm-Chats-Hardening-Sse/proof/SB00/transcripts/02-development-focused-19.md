# Synchronized development comparison

- head: `eb6be3ea38075b442d24976655f5c45ac08bd6b5`
- worktree: detached temporary worktree at `C:\repositories\CanDoItAll\.w\d`
- dependency mode: local sibling source projects
- host: Microsoft Windows 10.0.26200, x64; .NET SDK 10.0.303
- database: not used by this focused prior-failure slice

Command (the temporary root solution had the same three projects as the durable
`proof/SB00/prior-failures.slnx` reproduction solution):

```powershell
dotnet test .\.codex-sb00-prior-failures.slnx --configuration Release -p:UseLocalCanDoItAllLibraries=true -p:CanDoItAllComponentsRepositoryRoot=C:\repositories\CanDoItAll.Components -p:CanDoItAllFileToolsRepositoryRoot=C:\repositories\CanDoItAll.FileTools --filter "FullyQualifiedName~Workflow_api_exposes_agent_provider_options_for_llm_components|FullyQualifiedName~Agent_catalog_defaults_and_provider_save_validation_are_typed|FullyQualifiedName~Provider_save_known_metadata_failures_are_typed_request_validation|FullyQualifiedName~Provider_save_storage_failure_is_not_misclassified_as_request_validation|FullyQualifiedName~ExecutionUpdated_subscriber_failure_is_isolated_after_persistence|FullyQualifiedName~SendMessageAsync_records_current_startup_baseline|FullyQualifiedName~Organization_workspace_seeds_dotnet_app_delivery_skill_with_process_visible_browser_proof|FullyQualifiedName~StartProcessSubprocessAsync_supplies_dotnet_solution_setup_scaffold_contract_from_bound_solution_context|FullyQualifiedName~AgentService_GetStructureAsync_gates_external_runtime_capabilities_by_execution_authority|FullyQualifiedName~Generated_image_asset_create_uses_selected_provider_and_persists_image_asset|FullyQualifiedName~Generated_image_asset_create_lists_legacy_openai_image_provider_without_purpose_metadata|FullyQualifiedName~Late_previous_project_reload_cannot_replace_the_current_project_surface|FullyQualifiedName~Missing_project_routes_render_a_safe_structure_recovery_state|FullyQualifiedName~Edit_dialog_updates_runtime_node_without_structure_reload|FullyQualifiedName~Shipped_artifact_templates_declare_semantic_acceptance_contract|FullyQualifiedName~Provider_stays_below_the_post_extraction_line_ceiling" /m:1 --logger "console;verbosity=minimal"
```

Result: exit code 1; 11 passed, 8 failed, 0 skipped, 19 total.

| Project | Passed | Failed | Failed cases |
|---|---:|---:|---|
| Components | 1 | 4 | Both generated-image cases; late previous-project reload; missing-project recovery state. |
| Integration | 9 | 3 | Workspace seed; solution-setup scaffold contract; runtime-capability authority gate. |
| Unit | 1 | 1 | Provider line ceiling (`3620 > 3610`). |

All four prior Agent/Workflow API regressions and all five run-tracking cases passed. The edit-dialog
case and semantic-acceptance contract also passed.
