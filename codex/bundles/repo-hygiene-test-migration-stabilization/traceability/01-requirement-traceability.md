# Requirement Traceability

| Requirement | Owning Subbundle | Planned Proof |
|---|---|---|
| RH-001 | SB01 | Failing-first and passing `RepositoryTransientArtifactHygieneTests`; `git ls-files` source assertion for affected paths. |
| RH-002 | SB01 | Failing-first and passing `RepositoryNamingHygieneTests`; source assertion that active test methods/literals use behavior language. |
| RH-003 | SB02 | Passing `ProjectStructureRuntimeLauncherTests`; source assertion for current `src/App/CanDoItAll.Web` layout. |
| RH-004 | SB02 | Failing-first and passing `WorkspaceRuntimeProcessToolsTests.BuildWatchArgumentList_omits_no_restore_when_a_referenced_project_assets_file_is_stale`; realistic project-reference fixture. |
| RH-005 | SB03 | Passing `ProcessDefinitionCatalogProjectionTests.Dotnet_feature_code_change_keeps_browser_proof_out_of_atomic_targeted_validation_step`; semantic assertion notes. |
| RH-006 | SB03 | Passing three failed `ProcessRuntimeIntegrationAdapterTests`; added/retained negative proof for ambiguous branch outcome text. |
| RH-007 | SB04 | Isolated and order-specific database runtime-switch proof; no broad static-state leakage. |
| RH-008 | SB04 | `dotnet ef migrations has-pending-model-changes` transcript. |
| RH-009 | SB05 | Rebuild/start transcript plus HTTP/browser smoke for `http://localhost:5032`. |
| RH-010 | SB05 | Final execution report with full-suite outcome and any explicit remaining unrelated failures. |
