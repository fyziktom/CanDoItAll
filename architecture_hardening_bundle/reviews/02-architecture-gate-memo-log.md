# Architecture gate memo log

| Gate | Reviewed subbundles | Status | Decision | Corrective subbundle | Rerun required | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Gate A` | `01-03` | `Completed` | `Pass` |  | `No` | Canonical dependency meaning is now collection-first behind `ProcessDependencyCompatibilityBridge`, validation no longer mutates the editor model, and targeted integration/component proof passed without opening a corrective subbundle. |
| `Gate B` | `05-06` | `Completed` | `Pass` |  | `No` | Transaction/conflict rails are now explicit and differential persistence proof shows stable child identity plus rollback on forced child-identity conflicts, so downstream publication/runtime decomposition can proceed without a corrective persistence subbundle. |
| `Gate C` | `08-10` | `Completed` | `Pass` |  | `No` | Publication lifecycle vs clone, runtime transition orchestration, and read-side summary/query ownership are now materially separated enough to continue into consolidation and UI decomposition without opening a corrective runtime/query reset subbundle. |
| `Gate D` | `12-14` | `Completed` | `Pass` |  | `No` | Consolidation stayed narrow, the workspace decomposition remained browser-proofed, and schema hygiene is coherent enough for final closure without opening a corrective late-stage subbundle. |

## Gate A memo

### Gate

- `Gate A`

### Status

- `Completed`

### Reviewed subbundles

- `01-baseline-characterization-and-live-gap-reconciliation`
- `02-canonical-dependency-model-and-compatibility-boundary`
- `03-side-effect-free-validation-and-editor-normalization-split`

### Evidence reviewed

- Commands:
- `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\architecture_hardening_bundle --profile initiative --stage prepared`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessImportMetadataIntegrationTests|FullyQualifiedName~ProcessDeletionIntegrationTests|FullyQualifiedName~SqliteWriteCoordinationIntegrationTests" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --filter "FullyQualifiedName~ProcessesToolsTests|FullyQualifiedName~ProcessTemplateProjectionServiceTests|FullyQualifiedName~ProcessTemplateCatalogServiceTests|FullyQualifiedName~ProcessTemplatePackLoaderTests|FullyQualifiedName~ProcessTemplateMermaidExporterTests" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.GetEditorAsync_and_publish_clone_preserve_artifact_input_links|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_waits_for_all_dependencies_before_join_step_becomes_ready|FullyQualifiedName~ProcessesServiceIntegrationTests.Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale|FullyQualifiedName~ProcessesServiceIntegrationTests.ValidateDefinitionEditor_is_side_effect_free_for_stale_legacy_dependency_mirrors|FullyQualifiedName~ProcessesServiceIntegrationTests.NormalizeDefinitionEditor_is_idempotent_for_branching_and_dependency_shapes" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`
- Tests:
- Baseline integration/component/MCP proof passed.
- Subbundle 02 stale-legacy-scalar corruption proof passed.
- Subbundle 03 validator-purity and normalization-idempotence proof passed.
- Browser proof:
- N/A at this gate.
- Key diffs:
- `ProcessDependencyCompatibilityBridge` is the only compatibility boundary interpreting legacy dependency scalar fields.
- Save, publish, read, template-draft, runtime/workspace clone, and workspace editing paths now flow through canonical dependency collections.
- `ValidateDefinitionEditor` no longer normalizes in-place; save and workspace authoring now call explicit normalization helpers.
- Remaining open items:
- Optimistic concurrency is still absent.
- Persistence still deletes and recreates the child graph.
- Publication/runtime/query decomposition is still ahead.

### Architecture questions and answers

1. Question:
   - Is there now exactly one canonical dependency model?
   - Answer:
   - Yes. Core behavior now treats the dependency collection/row shape as the source of truth. Remaining scalar fields are mirror data behind `ProcessDependencyCompatibilityBridge`, not competing logic paths.
2. Question:
   - Is validation provably side-effect free?
   - Answer:
   - Yes. `ValidateDefinitionEditor` no longer normalizes in-place, and the new targeted proof invokes it directly against a stale legacy-mirror scenario without mutating the editor model.
3. Question:
   - Is the compatibility boundary narrow enough that later work can trust the model?
   - Answer:
   - Yes. The bridge now contains the legacy interpretation logic, and downstream save/publish/read/runtime/workspace behavior was rechecked against canonical dependency proof before this gate.

### Decision

- `Pass`

### If failed

- Corrective subbundle key:
- N/A
- Why downstream work is blocked:
- N/A
- Rerun commands:
- N/A

### Reviewer notes

- The next blocking work is transaction/concurrency hardening. Do not reopen dependency-model or validator-purity concerns in later phases unless new proof falsifies the current gate evidence.

## Gate B memo

### Gate

- `Gate B`

### Status

- `Completed`

### Reviewed subbundles

- `05-transaction-concurrency-and-conflict-hardening`
- `06-differential-definition-graph-persistence`

### Evidence reviewed

- Commands:
- `dotnet build C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.SaveAsync_rejects_stale_editor_concurrency_tokens_after_concurrent_update|FullyQualifiedName~ProcessesServiceIntegrationTests.PublishAsync_rejects_stale_publish_request_after_concurrent_update|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_rejects_stale_step_run_concurrency_token_after_prior_transition|FullyQualifiedName~ProcessesServiceIntegrationTests.StartRunAsync_prefills_project_role_binding_and_persists_runtime_signals|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_waits_for_all_dependencies_before_join_step_becomes_ready|FullyQualifiedName~ProcessesServiceIntegrationTests.Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale|FullyQualifiedName~MigrationBootstrapIntegrationTests.Bootstrap_migrates_a_new_managed_sqlite_database|FullyQualifiedName~SqliteWriteCoordinationIntegrationTests.Concurrent_activity_writes_complete_without_sqlite_lock_errors|FullyQualifiedName~SqliteWriteCoordinationIntegrationTests.Concurrent_search_index_writes_complete_without_sqlite_lock_errors" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.SaveAsync_preserves_child_ids_across_no_op_editor_round_trip|FullyQualifiedName~ProcessesServiceIntegrationTests.SaveAsync_targeted_step_update_preserves_unrelated_child_ids_and_artifact_links|FullyQualifiedName~ProcessesServiceIntegrationTests.SaveAsync_targeted_delete_removes_only_selected_branch_path_and_preserves_survivors|FullyQualifiedName~ProcessesServiceIntegrationTests.SaveAsync_rolls_back_graph_changes_when_child_identity_conflicts|FullyQualifiedName~ProcessesServiceIntegrationTests.PublishAsync_preserves_role_and_branch_canvas_positions_in_the_next_draft|FullyQualifiedName~ProcessesServiceIntegrationTests.Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.GetEditorAsync_and_publish_clone_preserve_artifact_input_links" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --filter "FullyQualifiedName~ProcessesToolsTests.ProcessesDefinitionSaveAsync_returns_successful_structured_content" -v:minimal`
- Tests:
- Subbundle 05 conflict and SQLite coordination proof passed.
- Subbundle 06 no-op save, targeted update, targeted delete, rollback-under-conflict, publish-clone compatibility, and MCP save regressions passed.
- Browser proof:
- N/A at this gate.
- Key diffs:
- Save, publish, start-run, and step-transition flows now have explicit transaction/conflict rails.
- `ProcessesService.SaveDefinitionChildrenAsync` is now a differential graph mutator instead of a delete-and-recreate rewrite.
- Child rows are matched by stable IDs first and only fall back to shape matching where the editor contract lacks child-row IDs.
- Remaining open items:
- Publication lifecycle and clone logic are still coupled.
- Version-number and slug-allocation race handling still need deeper decomposition work.
- Runtime policy/query decomposition is still ahead.

### Architecture questions and answers

1. Question:
   - Are transaction boundaries and conflict translation now explicit enough for the mutation core?
   - Answer:
   - Yes. Save, publish, start-run, and step-transition now all expose explicit rollback-aware boundaries and convert EF concurrency/uniqueness failures into module-level result errors instead of last-write-wins or raw exception leakage.
2. Question:
   - Does the differential save proof show stable logical identity rather than a quieter destructive rewrite?
   - Answer:
   - Yes. The new proof covers no-op saves, targeted updates, targeted deletes, and rollback after an injected child-identity conflict. Unchanged logical children keep their IDs, and the previous graph-wide delete/recreate path is no longer the normal save implementation.
3. Question:
   - Is the mutation core strong enough to support publication and runtime decomposition without opening a corrective persistence subbundle?
   - Answer:
   - Yes. The remaining risks are decomposition and race-hardening concerns in publication/runtime/query responsibilities, not unresolved mutation-core correctness gaps in save/concurrency behavior.

### Decision

- `Pass`

### If failed

- Corrective subbundle key:
- N/A
- Why downstream work is blocked:
- N/A
- Rerun commands:
- N/A

### Reviewer notes

- Proceed to publication/versioning/clone decomposition next. If later work reintroduces child-ID churn, hidden retries, or transaction leakage, reopen the gate immediately with a corrective persistence subbundle instead of patching around it downstream.

## Gate C memo

### Gate

- `Gate C`

### Status

- `Completed`

### Reviewed subbundles

- `08-publication-versioning-and-clone-engine-decomposition`
- `09-runtime-state-machine-and-transition-policy-extraction`
- `10-read-side-query-splitting-and-performance-hardening`

### Evidence reviewed

- Commands:
- `dotnet build C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.PublishAsync_preserves_role_and_branch_canvas_positions_in_the_next_draft|FullyQualifiedName~ProcessesServiceIntegrationTests.GetEditorAsync_and_publish_clone_preserve_artifact_input_links|FullyQualifiedName~ProcessesServiceIntegrationTests.PublishAsync_allocates_next_draft_version_after_the_highest_existing_version|FullyQualifiedName~ProcessesServiceIntegrationTests.Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale|FullyQualifiedName~ProcessesServiceIntegrationTests.PublishAsync_rejects_stale_publish_request_after_concurrent_update|FullyQualifiedName~ProcessDeletionIntegrationTests.DeleteAsync_removes_the_persisted_process_graph_and_search_document" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --filter "FullyQualifiedName~ProcessesToolsTests.ProcessesDefinitionPublishAsync_returns_structured_validation_failure" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_routes_selected_branch_and_skips_the_non_selected_path|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_waits_for_all_dependencies_before_join_step_becomes_ready|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_rejects_branch_outcome_selection_for_non_completed_transition|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_requires_branch_outcome_when_conditional_dependents_exist|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_rejects_stale_step_run_concurrency_token_after_prior_transition|FullyQualifiedName~ProcessesServiceIntegrationTests.StartRunAsync_prefills_project_role_binding_and_persists_runtime_signals|FullyQualifiedName~ProcessesServiceIntegrationTests.Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --no-build --filter "FullyQualifiedName~ProcessesToolsTests.ProcessesStepTransitionAsync_forwards_selected_branch_outcome" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.ListDefinitionsAsync_counts_roles_and_steps_from_the_current_summary_version_only|FullyQualifiedName~ProcessesServiceIntegrationTests.ListRunsAsync_returns_projected_step_progress_and_capability_gap_counts|FullyQualifiedName~ProcessesServiceIntegrationTests.StartRunAsync_prefills_project_role_binding_and_persists_runtime_signals|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_routes_selected_branch_and_skips_the_non_selected_path|FullyQualifiedName~ProcessesServiceIntegrationTests.Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale|FullyQualifiedName~ProcessesServiceIntegrationTests.GetEditorAsync_and_publish_clone_preserve_artifact_input_links|FullyQualifiedName~ProcessImportMetadataIntegrationTests.ImportAsync_imports_project_scoped_template_pack_processes" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --no-build --filter "FullyQualifiedName~ProcessesToolsTests.ProcessesStepTransitionAsync_forwards_selected_branch_outcome|FullyQualifiedName~ProcessesToolsTests.ProcessesDefinitionPublishAsync_returns_structured_validation_failure" -v:minimal`
- `codeanalytics snapshot`: `snap-20260413120220-53bec4ab`
- Tests:
- Publication/version proof passed, including next-draft allocation and MCP publish validation.
- Runtime proof passed, including branch-outcome enforcement, branch routing, dependency activation, stale-transition conflict handling, and MCP step transition forwarding.
- Read-side proof passed, including definition summary counts, projected run summary counts, editor/runtime read regressions, analytics coverage, and import/list correctness.
- Browser proof:
- N/A at this gate.
- Key diffs:
- `ProcessDefinitionDraftCloneEngine` now owns draft graph cloning while `ProcessesService.Publication.cs` keeps publication lifecycle and next-draft provisioning orchestration.
- `ProcessStepTransitionGuard`, `ProcessRuntimeProgressionPlanner`, and `ProcessRunStatusCalculator` now hold distinct runtime responsibilities instead of leaving the whole state-machine path inside one service method.
- `ProcessDefinitionListQueryService` and `ProcessRuntimeReadQueryService` now own the main definition/run/analytics read seams with filtered projections and grouped summaries rather than table-wide broad loads.
- Remaining open items:
- Shared helper/template consolidation is still ahead.
- Workspace/canvas decomposition is still ahead.
- Schema hygiene and long-file cleanup are still ahead.

### Architecture questions and answers

1. Question:
   - Are publication and clone responsibilities genuinely separated?
   - Answer:
   - Yes. The live code now has a dedicated clone engine (`ProcessDefinitionDraftCloneEngine`) and a thinner publication path that loads a publication context, validates publication, applies lifecycle state, and provisions the next draft through the clone engine. This is a real separation of concerns, not a rename of the same monolith.
2. Question:
   - Is runtime logic now materially more testable and decomposed?
   - Answer:
   - Yes. `TransitionStepAsync` still owns the public command surface and side-effect orchestration, but transition legality, branch-outcome validation, downstream progression/skip rules, and run-status recomputation now live in explicit internal helpers with direct regression proof. The transition hotspot is materially thinner than before subbundle 09.
3. Question:
   - Did the read-side split reduce broad-load assumptions without creating shadow truth?
   - Answer:
   - Yes. The new query services remain projection-only and reuse the canonical persisted data. They limit versions, roles, steps, runs, assignments, and analytics inputs to the filtered definitions/runs being queried, which is a real reduction in read-side breadth without introducing a second mutation model.

### Decision

- `Pass`

### If failed

- Corrective subbundle key:
- N/A
- Why downstream work is blocked:
- N/A
- Rerun commands:
- N/A

### Reviewer notes

- Proceed to shared helper/template consolidation next. If later phases re-concentrate publication/runtime/query logic back into `ProcessesService` or introduce query-side mutation, reopen Gate C immediately instead of patching around it downstream.

## Gate D memo

### Gate

- `Gate D`

### Status

- `Completed`

### Reviewed subbundles

- `12-template-subsystem-and-cross-module-shared-infrastructure-consolidation`
- `13-workspace-and-canvas-decomposition`
- `14-schema-hygiene-migrations-and-long-file-split`

### Evidence reviewed

- Commands:
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --filter "FullyQualifiedName~ProcessTemplateProjectionServiceTests|FullyQualifiedName~ProcessTemplateCatalogServiceTests|FullyQualifiedName~ProcessTemplatePackLoaderTests|FullyQualifiedName~ProcessTemplateMermaidExporterTests" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessDeletionIntegrationTests|FullyQualifiedName~MigrationBootstrapIntegrationTests.Bootstrap_migrates_a_new_managed_sqlite_database|FullyQualifiedName~ProcessesServiceIntegrationTests.GetEditorAsync_and_publish_clone_preserve_artifact_input_links|FullyQualifiedName~ProcessesServiceIntegrationTests.PublishAsync_preserves_role_and_branch_canvas_positions_in_the_next_draft|FullyQualifiedName~ProcessesServiceIntegrationTests.Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale" -v:minimal`
- `dotnet ef migrations script --project 'C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\CanDoItAll.Migrations.Sqlite.csproj' --startup-project 'C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\CanDoItAll.Migrations.Sqlite.csproj' --context AppDbContext | Out-Null`
- `dotnet ef migrations script --project 'C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj' --startup-project 'C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj' --context AppDbContext | Out-Null`
- Tests:
- Shared-helper and template proof passed, including the imported-child-ID normalization regression coverage.
- Workspace and canvas component proof passed.
- Schema-hygiene regression proof passed after regenerating synchronized SQLite and PostgreSQL migrations.
- Browser proof:
- `/processes` at `1600x900` and `430x932` confirmed the extracted Steps and Runs surfaces remained readable after the mobile stacked-layout fix. No schema-hygiene change invalidated that UI proof.
- Key diffs:
- Shared generic helpers remain in `CanDoItAll.SharedKernel`, while process-template role snapshot summary generation stays owned by `Processes`.
- `ProcessWorkspace` delegates the steps and runs tabs to focused children, presenter wrappers keep the state boundary in the parent, and canvas save now refreshes concurrency tokens from the persisted editor.
- `ProcessDefinitionModels.cs` was split into `ProcessDefinitionEnums.cs`, `ProcessDefinitionEntities.cs`, and `ProcessDefinitionEntityConfigurations.cs`. Stable aggregate-boundary foreign keys are explicit, both provider migrations are synchronized, and broader step-local foreign keys were deliberately rejected because real proof showed they destabilized the differential save core.
- Remaining open items:
- Final closure proof and bundle synchronization still remain ahead of this gate.

### Architecture questions and answers

1. Question:
   - Did the shared-helper consolidation reduce duplication without turning SharedKernel into a dumping ground?
   - Answer:
   - Yes. Only genuinely generic file, JSON, enum, and slug helpers moved to `CanDoItAll.SharedKernel`, while process-owned template summary behavior stayed inside the `Processes` module.
2. Question:
   - Is the workspace decomposition materially healthier and browser-proven rather than just split across more files?
   - Answer:
   - Yes. Steps and Runs moved into focused Razor components, presenter wrappers keep orchestration boundaries explicit, targeted component proof passed, and `/processes` browser proof confirmed the desktop and mobile surfaces remain readable.
3. Question:
   - Is schema hygiene coherent enough even though not every possible child relationship is enforced as a foreign key?
   - Answer:
   - Yes. Stable aggregate-boundary relationships are explicit and migrations are synchronized for both providers. Stronger step-local foreign keys were tested and rejected because they created real differential-save cycles, so leaving those rows application-managed is the smallest correct choice until a deeper persistence redesign is justified.

### Decision

- `Pass`

### If failed

- Corrective subbundle key:
- N/A
- Why downstream work is blocked:
- N/A
- Rerun commands:
- N/A

### Reviewer notes

- Final closure should preserve the current proof-backed schema boundary. Do not add broader step-local foreign keys later without first changing the mutation core and re-establishing save/delete proof.
