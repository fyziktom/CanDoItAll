# Prior stable-gate failure classification

Comparison heads:

- synchronized development: `eb6be3ea38075b442d24976655f5c45ac08bd6b5`
- synchronized feature/proof head: `5522880cbf3101ed54c216ab74cac3b8ff2bade0`
- prior implementation commit materialized after the original commitless run: `16b6aa4b60dc88a6134dd6c9c9e634c064ac5847`

The two focused commands selected 16 test methods and executed 19 cases because
`SendMessageAsync_records_current_startup_baseline` has four data rows.

| # | Test case | Development | Feature | Classification | Evidence | Action |
|---:|---|---|---|---|---|---|
| 1 | `CanDoItAll.Tests.Integration.WorkflowApiIntegrationTests.Workflow_api_exposes_agent_provider_options_for_llm_components` | Pass | Pass | ObsoleteAfterSync | `proof/SB00/transcripts/02-development-focused-19.md`; `03-feature-focused-19.md` | Prior feature-only global-converter regression is repaired by DTO-local conversion at the implementation commit. |
| 2 | `CanDoItAll.Tests.Integration.AgentApiFailureContractIntegrationTests.Agent_catalog_defaults_and_provider_save_validation_are_typed` | Pass | Pass | ObsoleteAfterSync | Same | Retain DTO-local conversion; no current failure. |
| 3 | `CanDoItAll.Tests.Integration.AgentApiFailureContractIntegrationTests.Provider_save_known_metadata_failures_are_typed_request_validation` | Pass | Pass | ObsoleteAfterSync | Same | Retain DTO-local conversion; no current failure. |
| 4 | `CanDoItAll.Tests.Integration.AgentApiFailureContractIntegrationTests.Provider_save_storage_failure_is_not_misclassified_as_request_validation` | Pass | Pass | ObsoleteAfterSync | Same | Retain DTO-local conversion; no current failure. |
| 5 | `CanDoItAll.Tests.Integration.AgentFrameworkExecutionRunTrackingIntegrationTests.ExecutionUpdated_subscriber_failure_is_isolated_after_persistence` | Pass | Pass | EnvironmentSensitive | Same | Preserve as stable-gate coverage; no SB00 product change. |
| 6 | `CanDoItAll.Tests.Integration.AgentFrameworkExecutionRunTrackingIntegrationTests.SendMessageAsync_records_current_startup_baseline(false, false)` | Pass | Pass | EnvironmentSensitive | Same | Preserve as stable-gate coverage; no SB00 product change. |
| 7 | `CanDoItAll.Tests.Integration.AgentFrameworkExecutionRunTrackingIntegrationTests.SendMessageAsync_records_current_startup_baseline(true, false)` | Pass | Pass | EnvironmentSensitive | Same | Preserve as stable-gate coverage; no SB00 product change. |
| 8 | `CanDoItAll.Tests.Integration.AgentFrameworkExecutionRunTrackingIntegrationTests.SendMessageAsync_records_current_startup_baseline(false, true)` | Pass | Pass | EnvironmentSensitive | Same | Preserve as stable-gate coverage; no SB00 product change. |
| 9 | `CanDoItAll.Tests.Integration.AgentFrameworkExecutionRunTrackingIntegrationTests.SendMessageAsync_records_current_startup_baseline(true, true)` | Pass | Pass | EnvironmentSensitive | Same | Preserve as stable-gate coverage; no SB00 product change. |
| 10 | `CanDoItAll.Tests.Integration.AgentFrameworkWorkspaceSeedIntegrationTests.Organization_workspace_seeds_dotnet_app_delivery_skill_with_process_visible_browser_proof` | Fail | Pass | Baseline | Same | Existing feature implementation resolves the development-baseline failure; retain the current behavior. |
| 11 | `CanDoItAll.Tests.Integration.ProjectStructureAgentIntegrationTests.StartProcessSubprocessAsync_supplies_dotnet_solution_setup_scaffold_contract_from_bound_solution_context` | Fail | Fail | Baseline | Same | Unrelated Project Structure baseline failure; do not change in this backend bundle. |
| 12 | `CanDoItAll.Tests.Integration.ProjectStructureAgentIntegrationTests.AgentService_GetStructureAsync_gates_external_runtime_capabilities_by_execution_authority` | Fail | Fail | Baseline | Same | Unrelated Project Structure baseline failure; do not change in this backend bundle. |
| 13 | `CanDoItAll.Tests.Components.ProjectStructurePageSimpleMutationTests.Generated_image_asset_create_uses_selected_provider_and_persists_image_asset` | Fail | Fail | Baseline | Same | Unrelated Project Structure component failure; do not change in this backend bundle. |
| 14 | `CanDoItAll.Tests.Components.ProjectStructurePageSimpleMutationTests.Generated_image_asset_create_lists_legacy_openai_image_provider_without_purpose_metadata` | Fail | Fail | Baseline | Same | Unrelated Project Structure component failure; do not change in this backend bundle. |
| 15 | `CanDoItAll.Tests.Components.ProjectStructurePageDatabaseSwitchTests.Late_previous_project_reload_cannot_replace_the_current_project_surface` | Fail | Fail | Baseline | Same | Unrelated Project Structure component failure; do not change in this backend bundle. |
| 16 | `CanDoItAll.Tests.Components.ProjectStructurePageDatabaseSwitchTests.Missing_project_routes_render_a_safe_structure_recovery_state` | Fail | Fail | Baseline | Same | Unrelated Project Structure component failure; do not change in this backend bundle. |
| 17 | `CanDoItAll.Tests.Components.ProjectStructurePageSimpleMutationTests.Edit_dialog_updates_runtime_node_without_structure_reload` | Pass | Pass | EnvironmentSensitive | Same | Prior broad-run-only failure; preserve as stable-gate coverage. |
| 18 | `CanDoItAll.Tests.Unit.Processes.ProcessTemplateCompatibilityHistoryTests.Shipped_artifact_templates_declare_semantic_acceptance_contract` | Pass | Pass | EnvironmentSensitive | Same | Prior isolated-artifact-layout failure; preserve as stable-gate coverage. |
| 19 | `CanDoItAll.Tests.Unit.Projects.ProjectStructureAgentRuntimeToolProviderArchitectureTests.Provider_stays_below_the_post_extraction_line_ceiling` | Fail | Fail | Baseline | Same | Unrelated baseline line-ceiling failure; do not expand its provider in this bundle. |

Summary: 8 Baseline, 7 EnvironmentSensitive, 4 ObsoleteAfterSync, 0 BranchInduced, 0 Unresolved.

Allowed classifications:

- Baseline
- BranchInduced
- EnvironmentSensitive
- ObsoleteAfterSync
- Unresolved

CP0 blocks on BranchInduced or Unresolved.
