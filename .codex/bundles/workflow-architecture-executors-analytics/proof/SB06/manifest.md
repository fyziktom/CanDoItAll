# SB06 Proof Manifest

- Subbundle ID: SB06
- Status: Completed
- Completed scope: isolated large-screen analytics presentation, schema-driven executor creation/editing, trusted renderer resolution, an isolated image-provider renderer, safe failure handling, focused component tests, and the combined 1600x1000 production browser flow
- Semantic invariant contract: bundle://proof/SB06/semantic-invariants.md

## Analytics UI Evidence

- Failing-first transcript: bundle://proof/SB06/transcripts/closure.txt
- Passing transcript: bundle://proof/SB06/transcripts/closure.txt
- Anti-stub transcript: bundle://proof/SB06/transcripts/closure.txt
- Failing-first: bundle://proof/SB06/failing-analytics.txt
- Passing component/build proof: bundle://proof/SB06/passing-analytics.txt
- Architecture and testability: bundle://proof/SB06/analytics-architecture.txt
- Component discovery: bundle://proof/SB06/component-discovery.txt
- Anti-stub audit: bundle://proof/SB06/anti-stub.txt
- Browser and visual review: bundle://proof/SB06/browser-validation.md

## Executor Settings Evidence

- Passing component/build proof: bundle://proof/SB06/passing-settings.txt
- Architecture and negative-source audit: bundle://proof/SB06/settings-architecture.md

## Named Component Tests

- `Panel_loads_lazily_and_uses_typed_all_then_selected_workflow_scope`
- `Panel_renders_typed_totals_beyond_recent_window_and_preserves_pricing_duration_and_model_semantics`
- `Panel_and_page_use_lazy_typed_projection_without_event_or_history_page_parsing`
- `Panel_logs_actionable_scope_and_shows_safe_message_when_query_fails`
- `Panel_ignores_older_query_that_completes_after_newer_refresh`
- `Trusted_image_renderer_edits_every_schema_field_and_filters_provider_capability`
- `Image_renderer_source_matches_the_builtin_trust_contract`
- `Image_renderer_keeps_saved_provider_neutral_until_capabilities_load`
- `Workflow_canvas_editor_has_no_executor_id_specific_settings_branches`
- `SettingsRendererHost_shows_failure_for_incomplete_renderer_claim`
- `RuntimePackageContributionRejectsSettingsPresentationModeDrift`
- plugin manifest custom-renderer contract and legacy-schema compatibility tests

## Analytics Slice Result

- `WorkflowAnalyticsPanel` consumes only `IWorkflowAnalyticsQueryService` and renders complete typed aggregates independently of the eight-row recent window.
- Default scope is all workflows; a typed All/SelectedWorkflow selector issues an exact `WorkflowId` query when selected.
- Known-free cost remains `$0.000000`, while unknown-pricing observations are displayed separately and never implied to be free.
- Duration total/average/minimum/maximum and final/active counts, state/backend totals, provider/model rows, and recent runs are visible.
- `WorkflowsPage` passes the query service into the isolated component, activates it only for the Analytics tab, and invalidates it after catalog/run mutations.
- Query failures log scope/workflow/refresh/request identifiers without payloads, show a fixed safe message, and cannot let an older completion overwrite a newer result.

## Executor Settings Slice Result

- Both the inspector and node-details modal route every descriptor through `SettingsRendererHost`; no StorageFile, HttpFetch, Spreadsheet, ProjectStructure, or ImageGeneration executor-ID branch remains.
- `WorkflowExecutorConfigurationMapper` remains the single schema/settings JSON translation path for built-in and plugin descriptors.
- `WorkflowSettingsRendererSource` allow-lists `builtin.image-generation` against its exact application trust, owner, and schema-version contract.
- `WorkflowExecutorSettingsPresentationMode` separates ordinary schema settings from custom-renderer claims; only ImageGeneration opts into the trusted renderer.
- Broken custom-renderer claims fail visibly instead of silently falling back; plugin validation and runtime parity enforce the same mode/renderer contract.
- `WorkflowImageGenerationSettingsRenderer` supplies the capability-aware provider picker, filters to image-generation providers, makes disabled providers non-selectable, preserves an unavailable saved provider honestly, and delegates every other field back to the declarative schema renderer.
- Provider discovery has a visible loading state, disposal cancellation, fixed safe failure text, and logs only a failure type rather than exception payload or message.
- The renderer layout reuses BaseLib `Stack`, `Grid`, `FormField`, `Alert`, and `LoadingState`; no new responsive or page-local CSS scope was added.

## Production Browser Result

- The production toolbox reports 19 runnable executors and keeps the governed `command.process` capability visibly planned rather than runnable.
- Document to Markdown and Spreadsheet are discoverable from the executor toolbox.
- Custom executors bypass the generic creation dialog; image-generation settings render immediately through the trusted renderer in Node setup.
- The Gmail plugin opens its schema-driven settings dialog above floating canvas windows after the desktop stacking-context repair.
- Analytics renders token dimensions, known cost, unknown pricing/usage, duration, provider/model rows, and recent runs from the typed projection.
- Current-session browser console errors: 0.
- Screenshot SHA-256: `503d9f58b8628222dad06cb264770cb769bd9e51a14a152b34f70d2a4cd60e52`, `2b17f634b8044421221311a823f6e45741eda3ef334c2c8698e8773a8ef90a08`, `b2415a2064a0d1523d9e60295b8033872839180bb95e1136863444855913257b`, `c1cb8cfd3e6c5c1d77d3cb3d969b3f877ec04603cbb4c9f7cc8a6733b6cb09a7`.

SB06 is complete. Browser actions, assertions, and screenshot inspection are durable in `bundle://proof/SB06/browser-validation.md`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Typed workflow analytics snapshot | `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowAnalyticsQueryService.cs` | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowAnalyticsPanel.razor` | `bundle://proof/SB06/passing-analytics.txt` and `bundle://proof/SB06/browser-validation.md` | stale-query, safe-error, unknown-pricing, and recent-window assertions in `bundle://proof/SB06/semantic-invariants.md` |
| Trusted executor settings presentation claim | executor/plugin descriptor projection in `bundle://proof/SB06/settings-architecture.md` | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowSettingsRendererSource.cs` and `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowImageGenerationSettingsRenderer.razor` | `bundle://proof/SB06/passing-settings.txt` and production browser proof in `bundle://proof/SB06/browser-validation.md` | key/trust/owner/schema/empty-schema/provider negatives in `bundle://proof/SB06/semantic-invariants.md` |
