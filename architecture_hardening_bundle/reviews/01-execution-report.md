# Execution report

## Status

- Execution state: `Completed`
- Current phase: `16-final-regression-proof-and-bundle-closure` completed on `2026-04-13`; subbundles `14-16` and Gate D are now closed

## Commands

### Prepared-stage validator
- `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\architecture_hardening_bundle --profile initiative --stage prepared`

### Core build
- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -v:minimal`

### Integration tests
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessImportMetadataIntegrationTests|FullyQualifiedName~ProcessDeletionIntegrationTests|FullyQualifiedName~SqliteWriteCoordinationIntegrationTests" -v:minimal`

### Component tests
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`

### MCP process tests
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --filter "FullyQualifiedName~ProcessesToolsTests|FullyQualifiedName~ProcessTemplateProjectionServiceTests|FullyQualifiedName~ProcessTemplateCatalogServiceTests|FullyQualifiedName~ProcessTemplatePackLoaderTests|FullyQualifiedName~ProcessTemplateMermaidExporterTests" -v:minimal`

### Subbundle 12 MCP/template proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --filter "FullyQualifiedName~ProcessTemplateProjectionServiceTests|FullyQualifiedName~ProcessTemplateCatalogServiceTests|FullyQualifiedName~ProcessTemplatePackLoaderTests|FullyQualifiedName~ProcessTemplateMermaidExporterTests" -v:minimal`

### Subbundle 12 integration proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests" -v:minimal`

### Subbundle 13 component proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`

### Subbundle 14 focused integration proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessDeletionIntegrationTests|FullyQualifiedName~MigrationBootstrapIntegrationTests.Bootstrap_migrates_a_new_managed_sqlite_database|FullyQualifiedName~ProcessesServiceIntegrationTests.GetEditorAsync_and_publish_clone_preserve_artifact_input_links|FullyQualifiedName~ProcessesServiceIntegrationTests.PublishAsync_preserves_role_and_branch_canvas_positions_in_the_next_draft|FullyQualifiedName~ProcessesServiceIntegrationTests.Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale" -v:minimal`

### Subbundle 14 migration script proof
- `dotnet ef migrations script --project 'C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\CanDoItAll.Migrations.Sqlite.csproj' --startup-project 'C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\CanDoItAll.Migrations.Sqlite.csproj' --context AppDbContext | Out-Null`
- `dotnet ef migrations script --project 'C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj' --startup-project 'C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj' --context AppDbContext | Out-Null`

### Subbundle 02 integration proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.GetEditorAsync_and_publish_clone_preserve_artifact_input_links|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_waits_for_all_dependencies_before_join_step_becomes_ready|FullyQualifiedName~ProcessesServiceIntegrationTests.Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale" -v:minimal`

### Subbundle 02 component proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`

### Subbundle 03 integration proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.GetEditorAsync_and_publish_clone_preserve_artifact_input_links|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_waits_for_all_dependencies_before_join_step_becomes_ready|FullyQualifiedName~ProcessesServiceIntegrationTests.Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale|FullyQualifiedName~ProcessesServiceIntegrationTests.ValidateDefinitionEditor_is_side_effect_free_for_stale_legacy_dependency_mirrors|FullyQualifiedName~ProcessesServiceIntegrationTests.NormalizeDefinitionEditor_is_idempotent_for_branching_and_dependency_shapes" -v:minimal`

### Subbundle 03 component proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`

### Subbundle 05 compile proof
- `dotnet build C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj -v:minimal`

### Subbundle 05 scoped proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.SaveAsync_rejects_stale_editor_concurrency_tokens_after_concurrent_update|FullyQualifiedName~ProcessesServiceIntegrationTests.PublishAsync_rejects_stale_publish_request_after_concurrent_update|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_rejects_stale_step_run_concurrency_token_after_prior_transition|FullyQualifiedName~ProcessesServiceIntegrationTests.StartRunAsync_prefills_project_role_binding_and_persists_runtime_signals|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_waits_for_all_dependencies_before_join_step_becomes_ready|FullyQualifiedName~ProcessesServiceIntegrationTests.Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale|FullyQualifiedName~MigrationBootstrapIntegrationTests.Bootstrap_migrates_a_new_managed_sqlite_database|FullyQualifiedName~SqliteWriteCoordinationIntegrationTests.Concurrent_activity_writes_complete_without_sqlite_lock_errors|FullyQualifiedName~SqliteWriteCoordinationIntegrationTests.Concurrent_search_index_writes_complete_without_sqlite_lock_errors" -v:minimal`

### Subbundle 06 compile proof
- `dotnet build C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj -v:minimal`

### Subbundle 06 scoped integration proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.SaveAsync_preserves_child_ids_across_no_op_editor_round_trip|FullyQualifiedName~ProcessesServiceIntegrationTests.SaveAsync_targeted_step_update_preserves_unrelated_child_ids_and_artifact_links|FullyQualifiedName~ProcessesServiceIntegrationTests.SaveAsync_targeted_delete_removes_only_selected_branch_path_and_preserves_survivors|FullyQualifiedName~ProcessesServiceIntegrationTests.SaveAsync_rolls_back_graph_changes_when_child_identity_conflicts|FullyQualifiedName~ProcessesServiceIntegrationTests.PublishAsync_preserves_role_and_branch_canvas_positions_in_the_next_draft|FullyQualifiedName~ProcessesServiceIntegrationTests.Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale" -v:minimal`

### Subbundle 06 artifact publish-clone proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.GetEditorAsync_and_publish_clone_preserve_artifact_input_links" -v:minimal`

### Subbundle 06 MCP proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --filter "FullyQualifiedName~ProcessesToolsTests.ProcessesDefinitionSaveAsync_returns_successful_structured_content" -v:minimal`

### Subbundle 08 compile proof
- `dotnet build C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj -v:minimal`

### Subbundle 08 scoped integration proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.PublishAsync_preserves_role_and_branch_canvas_positions_in_the_next_draft|FullyQualifiedName~ProcessesServiceIntegrationTests.GetEditorAsync_and_publish_clone_preserve_artifact_input_links|FullyQualifiedName~ProcessesServiceIntegrationTests.PublishAsync_allocates_next_draft_version_after_the_highest_existing_version|FullyQualifiedName~ProcessesServiceIntegrationTests.Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale|FullyQualifiedName~ProcessesServiceIntegrationTests.PublishAsync_rejects_stale_publish_request_after_concurrent_update|FullyQualifiedName~ProcessDeletionIntegrationTests.DeleteAsync_removes_the_persisted_process_graph_and_search_document" -v:minimal`

### Subbundle 08 MCP proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --filter "FullyQualifiedName~ProcessesToolsTests.ProcessesDefinitionPublishAsync_returns_structured_validation_failure" -v:minimal`

### Subbundle 09 compile proof
- `dotnet build C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj -v:minimal`

### Subbundle 09 scoped integration proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_routes_selected_branch_and_skips_the_non_selected_path|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_waits_for_all_dependencies_before_join_step_becomes_ready|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_rejects_branch_outcome_selection_for_non_completed_transition|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_requires_branch_outcome_when_conditional_dependents_exist|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_rejects_stale_step_run_concurrency_token_after_prior_transition|FullyQualifiedName~ProcessesServiceIntegrationTests.StartRunAsync_prefills_project_role_binding_and_persists_runtime_signals|FullyQualifiedName~ProcessesServiceIntegrationTests.Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale" -v:minimal`

### Subbundle 09 MCP proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --no-build --filter "FullyQualifiedName~ProcessesToolsTests.ProcessesStepTransitionAsync_forwards_selected_branch_outcome" -v:minimal`

### Subbundle 10 compile proof
- `dotnet build C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj -v:minimal`

### Subbundle 10 scoped integration proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.ListDefinitionsAsync_counts_roles_and_steps_from_the_current_summary_version_only|FullyQualifiedName~ProcessesServiceIntegrationTests.ListRunsAsync_returns_projected_step_progress_and_capability_gap_counts|FullyQualifiedName~ProcessesServiceIntegrationTests.StartRunAsync_prefills_project_role_binding_and_persists_runtime_signals|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_routes_selected_branch_and_skips_the_non_selected_path|FullyQualifiedName~ProcessesServiceIntegrationTests.Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale|FullyQualifiedName~ProcessesServiceIntegrationTests.GetEditorAsync_and_publish_clone_preserve_artifact_input_links|FullyQualifiedName~ProcessImportMetadataIntegrationTests.ImportAsync_imports_project_scoped_template_pack_processes\" -v:minimal`

### Subbundle 10 MCP proof
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj --no-build --filter "FullyQualifiedName~ProcessesToolsTests.ProcessesStepTransitionAsync_forwards_selected_branch_outcome|FullyQualifiedName~ProcessesToolsTests.ProcessesDefinitionPublishAsync_returns_structured_validation_failure" -v:minimal`

### Completed-stage validator
- `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\architecture_hardening_bundle --profile initiative --stage completed`

## Browser Artifacts

- `processes-subbundle13-steps-desktop-1600x900.png`
- `processes-subbundle13-runs-desktop-1600x900.png`
- `processes-subbundle13-steps-mobile-430x932.png`
- `processes-subbundle13-runs-mobile-430x932.png`
- `processes-final-steps-desktop-1600x900.png`
- `processes-final-runs-desktop-1600x900.png`
- `processes-final-steps-mobile-430x932.png`
- `processes-final-runs-mobile-430x932.png`

## Subbundle Gate Results

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
| --- | --- | --- | --- | --- | --- |
| `01-baseline-characterization-and-live-gap-reconciliation` | `Passed` | `Passed` | `Baseline integration, component, and MCP proof passed on the live repository.` | `Passed` | Prepared-stage validator passed. See `reviews/03-live-gap-baseline-memo.md` for the baseline coverage map and open architecture gaps. |
| `02-canonical-dependency-model-and-compatibility-boundary` | `Passed` | `Passed` | `Subbundle 03 can now treat dependency meaning as canonical and stable.` | `Passed` | `ProcessDependencyCompatibilityBridge` is now the only legacy dependency compatibility boundary. Save, publish, read, runtime, template draft, and workspace clone/edit paths now flow through canonical dependency collections, and the new integration proof deliberately corrupted persisted legacy scalar fields without breaking read/publish/runtime behavior. |
| `03-side-effect-free-validation-and-editor-normalization-split` | `Passed` | `Passed` | `Gate A can now review a stable canonical foundation without hidden validator mutation.` | `Passed` | `ValidateDefinitionEditor` is now side-effect free, save normalization is explicit through `NormalizeDefinitionEditorForSave`, workspace authoring normalization is routed through named helpers, and the new tests prove both validator purity and normalization idempotence. |
| `04-architecture-review-gate-a` | `Passed` | `Passed` | `Subbundle 05 may start; no corrective subbundle required.` | `Passed` | See `reviews/02-architecture-gate-memo-log.md` for the recorded decision and evidence summary. |
| `05-transaction-concurrency-and-conflict-hardening` | `Passed` | `Passed` | `Subbundle 06 can now replace destructive graph persistence on top of explicit transaction and conflict rails.` | `Passed` | Process aggregates now use provider-agnostic application-managed concurrency tokens, save/publish/start-run/step-transition flows have explicit transactions, provider migrations were generated for both stores, and nine scoped integration/coordination/bootstrap checks passed. An extra non-scope legacy baselining check also exposed `MigrationBootstrapIntegrationTests.Legacy_sqlite_database_is_baselined_and_preserves_existing_data` failing with `SQLite Error 1: 'table "Automation_DeadLetters" already exists'`; that issue sits in legacy bootstrap/schema handling rather than the process mutation surface hardened here. |
| `06-differential-definition-graph-persistence` | `Passed` | `Passed` | `Gate B can now review the mutation core before publication/runtime decomposition starts.` | `Passed` | `ProcessesService.SaveDefinitionChildrenAsync` now diffs the definition child graph in place instead of deleting and recreating it, child IDs stay stable across no-op saves, targeted updates, and targeted deletes, rollback proof passed after forcing a child-identity conflict, and publish-clone plus MCP save regressions remained green. |
| `07-architecture-review-gate-b` | `Passed` | `Passed` | `Subbundles 08-16 may proceed; no corrective persistence subbundle required.` | `Passed` | See `reviews/02-architecture-gate-memo-log.md` for the recorded decision and mutation-core evidence summary. |
| `08-publication-versioning-and-clone-engine-decomposition` | `Passed` | `Passed` | `Subbundle 09 can now extract runtime transition policy on top of a dedicated publish-lifecycle surface instead of a publish/clone monolith.` | `Passed` | `ProcessesService.PublishAsync` now loads a publication context instead of inlining the full graph query stack, `ProcessDefinitionDraftCloneEngine` owns draft graph cloning, next-draft numbering now follows the highest persisted version number, canonical dependency rows are re-materialized during clone through `ProcessDependencyCompatibilityBridge`, and the targeted publish/delete/MCP proof passed. |
| `09-runtime-state-machine-and-transition-policy-extraction` | `Passed` | `Passed` | `Subbundle 10 can now split the read-side on top of an explicit runtime transition guard, progression planner, and run-status calculator instead of a single transition hotspot.` | `Passed` | `ProcessesService.TransitionStepAsync` now loads a focused transition context and delegates guard validation to `ProcessStepTransitionGuard`, downstream activation and non-selected-path resolution to `ProcessRuntimeProgressionPlanner`, and run-status recomputation to `ProcessRunStatusCalculator`. The new branch-selection guard tests plus the existing runtime/MCP regression proof all passed.` |
| `10-read-side-query-splitting-and-performance-hardening` | `Passed` | `Passed` | `Gate C can now review publication/runtime/query decomposition against slimmer definition-list, run-summary, step-detail, and analytics read surfaces instead of broad table-wide loads.` | `Passed` | `ProcessesService` now delegates definition listing to `ProcessDefinitionListQueryService` and runtime reads to `ProcessRuntimeReadQueryService`. Definition list, run list, and analytics queries now project only the needed fields and limit downstream table loads to the filtered definitions/runs. The new run-summary regression plus the existing definition, editor, runtime, analytics, import, and MCP checks all passed.` |
| `11-architecture-review-gate-c` | `Passed` | `Passed` | `Subbundles 12-16 may proceed; no corrective runtime/query reset subbundle required.` | `Passed` | See `reviews/02-architecture-gate-memo-log.md` for the recorded decision. Gate C reviewed the publication clone split, runtime helper extraction, and read-side query services against snapshot `snap-20260413120220-53bec4ab` plus the targeted proof commands and judged the direction sound enough for consolidation and UI decomposition. |
| `12-template-subsystem-and-cross-module-shared-infrastructure-consolidation` | `Passed` | `Passed` | `Subbundle 13 can now decompose the workspace/canvas surfaces without re-carrying duplicated template/helper mechanics or the import-path child-ID conflict uncovered during consolidation proof.` | `Passed` | Shared generic helpers moved into `CanDoItAll.SharedKernel`, process-template role snapshot summary generation now lives in one `Processes` helper, and the required proof sweep also exposed and fixed an import normalization bug where exported child IDs could collide on re-import. The targeted MCP/template proof and the full `ProcessesServiceIntegrationTests` sweep both passed after the normalization fix. |
| `13-workspace-and-canvas-decomposition` | `Passed` | `Passed` | `Subbundle 14 may now start on top of the extracted steps/runs surfaces, the refreshed canvas-save concurrency tokens, and the mobile stacked-layout proof.` | `Passed` | `ProcessWorkspace` now delegates the steps and runs tabs to focused Razor children backed by explicit presenter wrappers, canvas persistence reloads the editor after successful saves so repeated canvas mutations keep fresh concurrency tokens, and a mobile-only tabs override lets the stacked 430x932 layout expand naturally instead of trapping the extracted surfaces inside a tiny internal panel. Focused component proof and real `/processes` Playwright checks both passed.` |
| `14-schema-hygiene-migrations-and-long-file-split` | `Passed` | `Passed` | `Gate D can now review a coherent model/configuration layout with synchronized provider migrations and proof-backed relationship boundaries.` | `Passed` | `ProcessDefinitionModels.cs` was split into `ProcessDefinitionEnums.cs`, `ProcessDefinitionEntities.cs`, and `ProcessDefinitionEntityConfigurations.cs`. Stable aggregate-boundary foreign keys are now explicit, broader step-local foreign keys were intentionally left application-managed because proof showed they destabilized differential save behavior, delete cleanup was updated accordingly, and the focused integration plus both provider migration-script checks passed. |
| `15-architecture-review-gate-d` | `Passed` | `Passed` | `Subbundle 16 may now close the bundle without opening a corrective late-stage subbundle.` | `Passed` | Gate D reviewed subbundles `12-14` and passed without corrective work. Shared helper extraction stayed narrow, the workspace decomposition retained strong desktop/mobile proof, and the schema-hygiene compromise is coherent with the current mutation core. |
| `16-final-regression-proof-and-bundle-closure` | `Passed` | `Passed` | `Final closure complete; the bundle is synchronized to shipped code and proof.` | `Passed` | Final solution build, targeted integration/component/MCP matrices, refreshed `/processes` browser proof, and the completed-stage validator all passed. The bundle report, gate memo log, README status, and raw-note closure table now match the shipped repository state. |

## Browser Validation Analytics

| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |
| --- | --- | --- | --- | --- | --- |
| `08-publication-versioning-and-clone-engine-decomposition` | `N/A` | `N/A` | `N/A` | `N/A` | `N/A (non-UI phase)` |
| `09-runtime-state-machine-and-transition-policy-extraction` | `N/A` | `N/A` | `N/A` | `N/A` | `N/A (non-UI phase)` |
| `10-read-side-query-splitting-and-performance-hardening` | `N/A` | `N/A` | `N/A` | `N/A` | `N/A (non-UI phase)` |
| `13-workspace-and-canvas-decomposition` | `/processes` | `1600x900` and `430x932` | `Opened the selected definition, exercised the extracted Steps and Runs tabs, kept the same Playwright page across both viewports, centered the floating selection windows, and used DOM geometry checks to confirm the open steps/runtime selection overlays stayed readable inside the viewport after the mobile layout fix.` | `processes-subbundle13-steps-desktop-1600x900.png`; `processes-subbundle13-runs-desktop-1600x900.png`; `processes-subbundle13-steps-mobile-430x932.png`; `processes-subbundle13-runs-mobile-430x932.png` | `Passed` |
| `16-final-regression-proof-and-bundle-closure` | `/processes` | `1600x900` and `430x932` | `Started the current source-backed web app, dismissed the database-profile modal, revisited the published definition workspace on the Steps and Runs tabs, and captured fresh desktop/mobile screenshots after the schema-hygiene changes to confirm the tab strip, canvas toolbars, and stacked mobile layout remained readable.` | `processes-final-steps-desktop-1600x900.png`; `processes-final-runs-desktop-1600x900.png`; `processes-final-steps-mobile-430x932.png`; `processes-final-runs-mobile-430x932.png` | `Passed` |

## Final screenshot review

- `processes-final-steps-desktop-1600x900.png`: the selected definition, Steps tab, and definition canvas toolbar are all visible at desktop width without clipping or overlap.
- `processes-final-runs-desktop-1600x900.png`: the Runs tab shows the runtime canvas alongside the selected published definition without collapsing the workspace shell.
- `processes-final-steps-mobile-430x932.png`: the stacked mobile layout keeps the tab strip and definition canvas readable instead of trapping the extracted panel inside a tiny internal scroller.
- `processes-final-runs-mobile-430x932.png`: the mobile Runs view keeps the runtime canvas readable and the horizontal tab strip remains operable on narrow width.

## Analytics Review

- Bundle readiness repair is complete and the prepared-stage validator now passes on the live repository.
- Baseline proof is anchored by `reviews/03-live-gap-baseline-memo.md`.
- Canonical dependency handling is now stabilized behind `ProcessDependencyCompatibilityBridge`; the live proof includes a persisted stale-legacy-scalar corruption check that still round-trips and runs correctly through the canonical dependency rows.
- Validation is now side-effect free. Explicit normalization entry points are `ProcessesService.NormalizeDefinitionEditorForSave`, `ProcessWorkspace.NormalizeEditorForAuthoring`, and `ProcessWorkspace.NormalizeStepDraftForAuthoring`.
- Process definitions, definition versions, runs, and step runs now carry application-managed concurrency tokens stamped by `AppDbContext`; save, publish, run-start, and step-transition paths translate concurrency and unique conflicts into explicit module errors instead of leaking raw EF exceptions.
- Subbundle 05 proof passed with a scoped nine-test sweep covering stale save/publish/transition conflicts, one downstream runtime flow, managed-SQLite migration bootstrap, and SQLite write coordination.
- Subbundle 06 replaced the destructive child-graph rewrite in `ProcessesService.SaveDefinitionChildrenAsync` with tracked differential persistence. Roles, skills, steps, branch outcomes, dependencies, role assignments, artifact expectations, and artifact inputs are now matched by stable IDs first and by shape only where the editor lacks child-row IDs.
- The differential save proof passed: no-op save stability, targeted update stability, targeted delete stability, rollback after an injected child-identity conflict, existing publish-clone compatibility, and the MCP save regression all stayed green.
- Subbundle 08 is now complete. Publication lifecycle and draft graph cloning no longer live in one broad method, `ProcessDefinitionDraftCloneEngine` owns the cloned role/step/dependency/artifact graph, canonical dependency rows are rebuilt during clone instead of depending on stale legacy scalar fields, and the new integration regression proves next-draft allocation advances past the highest persisted version instead of failing on `published + 1` collisions.
- Subbundle 09 is now complete. Runtime transition orchestration is materially thinner, transition legality and branch-outcome enforcement live in `ProcessStepTransitionGuard`, downstream activation/skip propagation live in `ProcessRuntimeProgressionPlanner`, and run-status recomputation moved into `ProcessRunStatusCalculator`. The targeted runtime proof passed with two new branch-guard regressions plus the existing runtime/MCP checks.
- Subbundle 10 is now complete. Read-side aggregation no longer assumes small tables: `ProcessDefinitionListQueryService` filters versions/roles/steps/runs to the selected definitions before composing list items, `ProcessRuntimeReadQueryService` uses grouped step-run summaries for run lists, and analytics now project only run/step/conformance scalar fields instead of hydrating full entities for common summary views. The definition-count, run-summary, editor/runtime regression, analytics, import, and MCP proof all passed.
- Gate C passed without a corrective subbundle. Publication lifecycle vs clone, runtime transition policy, and read-side projection ownership are now separated enough that the next phases can focus on shared-helper/template consolidation and UI decomposition instead of reopening the core mutation/query architecture.
- Subbundle 12 is now complete. Generic file-root lookup, JSON loading, enum parsing, and file-safe slug generation were extracted into `CanDoItAll.SharedKernel` because their semantics are genuinely cross-module and stable, while process-template role snapshot summary generation moved into a single `Processes` helper because it remains domain-owned behavior.
- The subbundle 12 proof sweep also exposed a real import-path defect outside the original helper duplication target: exported process envelopes were retaining child graph IDs, so importing a clone into a new definition could fail with `processes.definition-unique-conflict`. `ProcessesService.ImportAsync` now normalizes imported child IDs and remaps internal references before save instead of silently relying on persisted graph identity.
- Subbundle 12 proof passed with the required MCP/template sweep plus the full `ProcessesServiceIntegrationTests` run, so downstream workspace decomposition can now proceed on a thinner helper surface and a safer import path.
- Subbundle 13 is now complete. `ProcessWorkspace` delegates the steps and runs surfaces to focused child Razor components, explicit presenter wrappers keep the workspace state boundary in the parent instead of pushing domain rules into markup, and `PersistDefinitionCanvasChangesAsync` now reloads the saved editor so repeated canvas mutations keep fresh concurrency tokens instead of silently diverging from persisted state after the first save.
- The required browser proof for subbundle 13 surfaced a real narrow-layout issue: on `430x932`, the stacked detail shell and fill-height tabs trapped the extracted surfaces inside a tiny internal scroller that clipped the selection window. A mobile-only `ProcessWorkspace.razor.css` override now lets the tabs panel grow naturally on small screens, and the rerun proof confirmed both the steps and runs selection overlays fit within the viewport after centering.
- Subbundle 14 is now complete. `ProcessDefinitionModels.cs` was split into `ProcessDefinitionEnums.cs`, `ProcessDefinitionEntities.cs`, and `ProcessDefinitionEntityConfigurations.cs`, stable aggregate-boundary foreign keys are explicit in the EF model, both provider migrations were regenerated, and the focused regression plus migration-script proof passed.
- The schema-hygiene proof also forced a deliberate architectural compromise: broader step-local foreign keys were tested and rejected because they introduced real differential-save cycle failures. Those rows remain application-managed for now, with explicit delete cleanup in `ProcessesService.Publication.cs`, which is the smallest correct choice until a deeper persistence redesign is justified.
- Gate D passed without a corrective subbundle. Shared extraction stayed narrow, the workspace decomposition remained clear and browser-proofed, and the final schema/migration layout is coherent enough for closure.
- Final closure proof is now complete. The full solution build passed, the targeted integration, component, and MCP process matrices passed, `/processes` browser proof was refreshed on the final source state at `1600x900` and `430x932`, and the completed-stage validator passed after the bundle documents were synchronized.
- Current warnings are external to this bundle scope: `xUnit2031` in `WorkforceProfileIntegrationTests.cs` and `ASP0006` in `TabsComponentTests.cs` appeared during the baseline test sweep and did not affect the targeted Process-module tests.
- An additional non-scope validation check uncovered a legacy baselining failure in `MigrationBootstrapIntegrationTests.Legacy_sqlite_database_is_baselined_and_preserves_existing_data` with `SQLite Error 1: 'table "Automation_DeadLetters" already exists'`. That failure occurs in legacy bootstrap/schema handling, not in the process concurrency flows touched by subbundle 05.
- Browser analytics are now recorded for subbundle 13 and final closure.
- Gate A passed without a corrective subbundle, and subbundles 05-06 now give downstream persistence/runtime refactors both explicit conflict rails and stable child identity instead of silent last-write-wins or graph churn.
- Gate B also passed without opening a corrective subbundle, and subbundle 08 removed the remaining publish/clone coupling that would have weakened downstream runtime extraction. Downstream work is now blocked only on the planned runtime/query decomposition phases rather than on unresolved mutation-core or publish-version safety concerns.
- Gate C also passed without opening a corrective subbundle, so downstream work moved into the planned template/shared-helper, workspace, and schema-hygiene phases rather than reopening publication/runtime/query concentration.

## Architecture gate summary

See `reviews/02-architecture-gate-memo-log.md`.

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| `U001` Review the new Process module | `Solved` | `reviews/03-live-gap-baseline-memo.md`, `reviews/01-execution-report.md`, and `reviews/02-architecture-gate-memo-log.md` now cover the live baseline, each hardening phase, and the final closure proof. |
| `U002` Check duplication across modules | `Solved` | Subbundle `12` consolidated the real shared helper hotspots into `CanDoItAll.SharedKernel` only where semantics were genuinely cross-module, centralized process-template role snapshot summary generation inside `Processes`, and Gate D confirmed that this stayed a narrow ownership improvement instead of a dumping-ground extraction. |
| `U003` Focus on architecture, long files, testability, canonicality, performance, DB conflicts | `Solved` | Subbundles `02`, `03`, `05`, `06`, `08`, `09`, `10`, `12`, `13`, and `14` now cover canonical dependency meaning, validation purity, process-module DB conflict hardening, differential graph persistence with rollback proof, publication/version decomposition with conflict-aware next-draft allocation, runtime transition policy extraction, read-side query hardening, shared-infrastructure consolidation, workspace/canvas decomposition with browser proof, and the remaining schema/configuration split plus migration hygiene. Gate D and the final proof matrix both passed. |
| `U004` Produce a detailed execution-grade bundle | `Solved` | Prepared-stage validator passed on `2026-04-13`; bundle contract repaired and execution started. |
| `U005` Detailed Codex-ready subbundles | `Solved` | Subbundle READMEs, task files, and corrective playbooks are now validator-compliant and executable. |
| `U006` Use bundle examples and improve on them | `Solved` | Bundle examples were incorporated and strengthened with live readiness repair plus corrective governance. |
| `U007` Add repeated architecture reviews and corrective paths | `Solved` | Review gates and corrective playbooks exist and the corrective playbooks are now execution-valid. |
| `U008` Deliver as zip | `Solved` | Zip packaging was created for the preparation artifact, and the live execution bundle is now fully synchronized and validator-clean. |

## Residual risks

- Legacy dependency scalar columns still exist in the schema as mirror fields behind `ProcessDependencyCompatibilityBridge`; they are no longer a distributed source of truth, but a later cleanup phase is still required if the legacy columns are to be removed entirely.
- Step-local child rows remain application-managed rather than fully database-enforced because broader foreign keys produced real differential-save cycle failures. Any future attempt to tighten those constraints should start with a persistence-model redesign, not another migration-only tweak.
- Legacy SQLite baselining still shows a separate bootstrap/schema issue in `MigrationBootstrapIntegrationTests.Legacy_sqlite_database_is_baselined_and_preserves_existing_data`; that remains outside this bundle’s Process-module scope.
