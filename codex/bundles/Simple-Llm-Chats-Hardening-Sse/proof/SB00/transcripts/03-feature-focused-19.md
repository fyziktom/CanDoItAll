# Synchronized feature comparison

- head: `5522880cbf3101ed54c216ab74cac3b8ff2bade0`
- development merged: `eb6be3ea38075b442d24976655f5c45ac08bd6b5`
- dependency mode: local sibling source projects
- host: Microsoft Windows 10.0.26200, x64; .NET SDK 10.0.303
- database: not used by this focused prior-failure slice

Command:

```powershell
dotnet test .\.codex-sb00-prior-failures.slnx --configuration Release --no-build --no-restore -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName~Workflow_api_exposes_agent_provider_options_for_llm_components|FullyQualifiedName~Agent_catalog_defaults_and_provider_save_validation_are_typed|FullyQualifiedName~Provider_save_known_metadata_failures_are_typed_request_validation|FullyQualifiedName~Provider_save_storage_failure_is_not_misclassified_as_request_validation|FullyQualifiedName~ExecutionUpdated_subscriber_failure_is_isolated_after_persistence|FullyQualifiedName~SendMessageAsync_records_current_startup_baseline|FullyQualifiedName~Organization_workspace_seeds_dotnet_app_delivery_skill_with_process_visible_browser_proof|FullyQualifiedName~StartProcessSubprocessAsync_supplies_dotnet_solution_setup_scaffold_contract_from_bound_solution_context|FullyQualifiedName~AgentService_GetStructureAsync_gates_external_runtime_capabilities_by_execution_authority|FullyQualifiedName~Generated_image_asset_create_uses_selected_provider_and_persists_image_asset|FullyQualifiedName~Generated_image_asset_create_lists_legacy_openai_image_provider_without_purpose_metadata|FullyQualifiedName~Late_previous_project_reload_cannot_replace_the_current_project_surface|FullyQualifiedName~Missing_project_routes_render_a_safe_structure_recovery_state|FullyQualifiedName~Edit_dialog_updates_runtime_node_without_structure_reload|FullyQualifiedName~Shipped_artifact_templates_declare_semantic_acceptance_contract|FullyQualifiedName~Provider_stays_below_the_post_extraction_line_ceiling" /m:1 --logger "console;verbosity=minimal"
```

Result: exit code 1; 12 passed, 7 failed, 0 skipped, 19 total.

| Project | Passed | Failed | Failed cases |
|---|---:|---:|---|
| Components | 1 | 4 | Same four development-baseline component failures. |
| Integration | 10 | 2 | The two Project Structure agent integration failures. |
| Unit | 1 | 1 | The same provider line-ceiling failure. |

The workspace-seed case passed on the feature head. Every prior feature-induced Agent/Workflow API
regression passed. The seven current failures match development-baseline failures and are outside this
bundle's LLM Chats backend scope.
