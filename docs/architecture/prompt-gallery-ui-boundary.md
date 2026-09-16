# Prompt Gallery UI boundary

Maintained record of the Prompt Gallery presentation decoupling on `components-decoupling`:
architectural decisions, the behavior matrix, the validation performed, and open items.
Historical Agents execution records remain in `modules-decoupling`; this document is the
single maintained record for the Prompt Gallery seam.

## Ownership

| Responsibility | Owner | Notes |
|---|---|---|
| Rendering of the search list, editor form and compatibility warning | `src/UI/CanDoItAll.Prompts.UI` | Controlled surfaces: explicit presentation records in, typed intents out; no `[Inject]`; scoped CSS moved with the markup |
| Public Gallery value contracts, enums, `IPromptGalleryService`, Curator ports | `src/Modules/CanDoItAll.Modules.Prompts.Contracts` | Same `CanDoItAll.Modules.Prompts` namespace; only the declaring assembly changed |
| Search requests, debounce, request generations, favorite writes | `PromptGallerySearchSession` + `PromptGallerySearchHost` (module) | One session per host instance; never a scoped service |
| Editing target, loads, draft/version/archive/suppression writes, identity adoption | `PromptGalleryEditorSession` + `PromptGalleryItemEditorHost` (module) | Reports `PromptGalleryEditorCommit` to its owner instead of mutating its own parameter |
| Picker dialog lifetime, immutable-version selection, nested editor refresh | `PromptGalleryPickerDialog` (module) | Closes only its own `DialogReference` |
| Picker button and chat composer effects, compatibility check, consent, suppression | `PromptGalleryPickerButton`, `PromptGalleryChatComposerButton` (module) | Lifetime tokens passed to `DialogService.OpenAsync`; context generation fences late results |
| Route, `promptId` request, editor dialog, catalog invalidation, optional Curator | `PromptGalleryPage` (module) | Curator port unchanged; `AgentFrameworkPromptGalleryCuratorLauncher` stays with the Agents owner |
| Validation, optimistic concurrency, persistence, projections, activity | `PromptsService` (module) | Unchanged |
| Backend-free scenario host | `src/Sandboxes/CanDoItAll.Prompts.UiSandbox` | Renders the same surfaces with deterministic local state |

### Contract decision

The rendering library needs the Gallery enums, `PromptGallerySearchItem`,
`PromptGalleryItemDetails` (mapped to an immutable editor source at the boundary),
`PromptGalleryVersionInfo`, `PromptWarningSuppression`, `PromptProviderModel`,
`PromptModelRecommendations`, `PromptCompatibilityResult` and `PromptCompatibilityIssue`.
Duplicating those as UI DTOs would have meant five enum copies plus two-way mapping of a
dozen records with no isolation benefit, so the existing public contracts moved out of the
module into a feature-owned contracts assembly that references only `CanDoItAll.SharedKernel`.
Consumers compile unchanged and JSON payloads are identical because the namespace is kept.
`PromptGallerySelection` stays in the module: it is a host-level result consumed by the
Workflow canvas editor, not something the renderer needs. `PromptCompatibilityWarningDecision`
moved to the rendering library because the warning surface emits it.

Evaluated compile graph of `CanDoItAll.Prompts.UI` (project references only, sibling source
mode for Components): `CanDoItAll.Modules.Prompts.Contracts` → `CanDoItAll.SharedKernel`;
`CanDoItAll.Components.BaseLib` → `CanDoItAll.Components.Common`;
`Microsoft.AspNetCore.Components.Web`. The guard test `PromptsUiBoundaryTests` asserts the
referenced-assembly set, the absence of `[Inject]` on every component, and that public
signatures expose no other assembly. The sandbox references only the rendering library;
`PromptsSandboxTests` asserts its referenced assemblies exclude the module, Infrastructure,
Entity Framework, Web, AppComponents and AgentFramework.

## Behavior matrix

Classification: **Preserve** = intended current behavior kept; **Safeguard** = authorized
isolation fix confirmed against the baseline code and covered by regression tests;
**Correction** = explicit observable change recorded here.

| Behavior | Baseline owner → new owner | Class | Test |
|---|---|---|---|
| Text/tags/kind/status/favorites/archive filters, pagination, Compact/PageSize, provider/model rules, "Show actual chat model only", Clear filters | `PromptGallerySearchList` → session + surface | Preserve | `PromptGallerySearchSessionTests` (pinned provider/model, desktop local filters), `PromptGallerySearchSurfaceTests`, `PromptGallerySearchHostTests.Actual_chat_model_filter…` |
| Late search success from a superseded request | list `operation` reference check → generation fence | Preserve | `Stale_success_does_not_replace_the_current_page_or_clear_busy` |
| Late search **failure** or `finally` from a superseded request cleared busy / raised an error | unguarded in baseline → fenced by generation | Safeguard | `Stale_failure_does_not_publish_an_error_or_clear_busy`, host `Old_search_failure_after_a_newer_request…` |
| Pinned context change while a load is in flight was remembered but issued no request | `if (!isLoading)` in baseline → always replaces the operation | Safeguard | `Pinned_context_change_during_load_issues_a_new_query…`, host `Context_change_while_loading…` |
| No-op parameter echo does not reload | Preserve | Preserve | `Unchanged_context_echo_and_display_only_changes_do_not_reload` |
| Search debounce (250 ms) without timing-fragile tests | `Task.Delay` → injectable delay delegate | Preserve | `Typed_text_is_debounced_and_only_the_latest_value_is_queried` |
| Backend that ignores cancellation and completes late | n/a → fenced by generation, not by token | Safeguard | stale-success/failure tests use a fake that ignores the token |
| Favorite write result vs. follow-up reload failure | same notice path → distinct notices | Safeguard | `Favorite_write_success_with_failed_reload_reports_a_refresh_problem…`, `Favorite_write_failure_keeps_the_page…` |
| Editor phases Closed/New/Loading/Ready/Missing-Failed; a missing record never becomes a new draft | `loadError` string → `PromptGalleryEditorPhase` | Preserve/Safeguard | `Missing_item_is_failed_with_its_requested_identity_and_retry_reloads_it`, host `Missing_item_shows_a_failed_state…`, surface `Failed_phase_shows_retry…` |
| Target change A→B during a read invalidates A's effects | unguarded → generation fence | Safeguard | `Target_change_during_load_ignores_the_late_result…` |
| Dispose invalidates reads and presentation effects but does not roll back a committed write | n/a | Safeguard | `Disposal_ignores_a_late_read`, `Late_save_result_after_dispose_is_not_published_and_the_write_is_not_replayed` |
| Same target + normal rerender keeps EditContext, validation and in-progress fields | baseline reloaded on parameter echo after save (`ItemId` self-mutation) → source-reference reset only | Safeguard | surface `Rerender_with_the_same_source_preserves_edits…`, host `Owner_echo_of_the_adopted_identity…` |
| First save identity: baseline set `ItemId`, cleared `loadedItemId`, then reloaded; a failed reload lost the identity | receipt adopted before any refresh | Safeguard | `First_save_adopts_identity_and_token_from_the_receipt_even_when_the_re_read_fails`, host `First_save_then_failed_re_read_then_second_save…` |
| Repeated Save cannot create a second record; duplicate submit while busy is ignored | busy only on the button → session-level command coordination | Safeguard | `Duplicate_submit_while_a_save_is_in_flight_is_ignored`, surface `Busy_presentation_blocks_duplicate_commands` |
| Submission snapshot independent of later edits and nested collections | n/a → `PromptGalleryEditorSubmission` copies | Safeguard | `PromptGalleryEditorFormTests.Submission_snapshot_is_independent…`, surface `Submit_requires_name_and_content_and_emits_an_independent_snapshot` |
| Interaction policy during a command: the whole form is disabled (`fieldset disabled`), not only the button | Correction (policy made explicit) | Correction | surface `Busy_presentation_blocks_duplicate_commands`, sandbox `editor-busy` |
| `ExpectedUpdatedAtUtc` and concurrency conflicts; no automatic overwrite | Preserve | Preserve | `Concurrency_conflict_keeps_the_draft_identity_and_token_without_overwriting` |
| "Create final version" = save draft, then create version; "draft saved, finalization failed" is distinct from "nothing saved"; busy spans the whole command; the catalog is told about the committed draft | baseline busy ended after `SaveCore` | Safeguard | `Finalize_reports_draft_saved_when_version_creation_fails`, `Finalize_success_commits_draft_then_version…`, host `Finalize_partial_success…` |
| Unknown persistence outcome: keep the draft, no identity guess, no blind replay, explicit warning | generic catch → warning state | Safeguard | `Unknown_save_outcome_keeps_the_draft_without_an_identity_and_warns_before_any_retry` |
| Archive/restore advance the concurrency token; the editor re-reads it (baseline left a stale token so the next save conflicted) | Safeguard | Safeguard | `Archive_toggle_commits_and_re_reads_the_token_for_the_next_save` |
| Warning suppression persisted immediately, independent of Save draft | Preserve | Preserve | `Warning_suppression_is_persisted_immediately…` |
| Picker: item with a final version inserts the exact immutable snapshot; missing/failed version never falls back to the draft | Preserve | Preserve | `Selection_with_a_final_version_inserts_the_immutable_snapshot`, `Missing_final_version_does_not_substitute_the_draft`, `Draft_only_item_inserts_the_draft_content` |
| Picker closes only itself; EditRequested override closes first, then invokes | `DialogService.CloseAsync()` (last dialog) → cascaded `DialogReference` | Safeguard | `Edit_requested_override_closes_picker_before_invoking_callback`, `Nested_editor_commit_refreshes_the_picker_list_and_leaves_unrelated_dialogs_open` |
| Nested editor commit refreshes the picker's own list in place | remount-free `RefreshAsync` | Preserve | same |
| Dialog ownership tied to the real `DialogService.OpenAsync` lifetime; disposing the owner closes only its dialogs | no token in baseline → lifetime token | Safeguard | `Disposing_the_picker_button_closes_only_its_own_dialog…`, composer `Disposing_the_composer_closes_its_warning_dialog_and_never_inserts` |
| Compatibility check before insertion; blocking error offers only Cancel; Insert anyway / Insert and suppress semantics; suppression failure is not an insertion failure; nothing inserted without consent | Preserve | Preserve | composer tests `Incompatible_selection_opens_dialog_and_cancel_does_not_emit_content`, `Insert_and_suppress_persists_preferences_and_a_rejected_preference_does_not_block_insertion` |
| Provider/model change or owner disposal while a selection is being evaluated never inserts into a different target | none → context generation + lifetime token | Safeguard | `Provider_or_model_change_while_the_warning_is_open_drops_the_stale_selection` |
| `/prompt-gallery` route and `promptId` query; A→null→A and A→missing→A reopen; the same value echoed by a rerender does not reopen a closed editor | baseline acknowledgement never cleared, so A→null→A did not reopen | Safeguard | `PromptId_request_reopens_after_null_and_after_a_missing_item_but_not_on_an_echo` |
| Catalog invalidation after an editor commit | `@key` remount (reset filters and page) → `RefreshAsync` (preserves filters and page, clamps to the last page) | Correction | `Editor_commit_refreshes_the_list_in_place_and_preserves_filters`, `Refresh_reloads_the_current_page_and_clamps…` |
| Curator optional, context activated/synchronized, lease released, presentation retry, functional UI without Curator | Preserve; activation failure no longer throws out of the page; presentation load no longer delays `promptId` handling | Safeguard | `PromptGalleryPageTests` Curator facts |
| Required name/content before saving | toast (`NotificationService.Warning`) → inline validation alert in the surface; backend validation results still arrive as toasts | Correction | surface `Submit_requires_name_and_content…` |
| "Add model" provider/model required and duplicate detection | toast → inline message | Correction | `PromptGalleryEditorFormTests.Supported_models_are_deduplicated…` |

Explicitly recorded corrections: in-place refresh instead of remount (filters/page survive),
inline required-field and add-model validation instead of toasts, and the whole-form busy
policy. No delete, schema, routing, or URL contract was added.

## Consumers

- `PromptGalleryPage` (`/prompt-gallery`) renders `PromptGallerySearchHost` and, inside the
  declarative BaseLib `Dialog`, `PromptGalleryItemEditorHost`.
- `PromptGalleryPickerDialog` renders `PromptGallerySearchHost` in compact mode and opens
  `PromptGalleryItemEditorHost` as a nested dialog when no `EditRequested` override exists.
- `AgentChatPanel`, `PromptGalleryLlmChatComposerActionContributor` (AgentFramework) keep
  using `PromptGalleryChatComposerButton`; `WorkflowCanvasEditor` keeps using
  `PromptGalleryPickerButton` and `PromptGallerySelection`. No consumer source changed.
- The sandbox renders `PromptGallerySearchSurface`, `PromptGalleryItemEditorSurface` and
  `PromptCompatibilityWarningSurface` directly.

## Validation

Commands run from the repository root in Release with `/m:1`. Discovery counts were stated
before execution.

| Slice | Filter | Expected / discovered | Result |
|---|---|---|---|
| Unit sessions and form policy | `FullyQualifiedName~CanDoItAll.Tests.Unit.Prompts.` | 34 / 34 | 34 passed |
| Components surfaces, hosts, dialogs, page, boundary, sandbox | `FullyQualifiedName~CanDoItAll.Tests.Components.Prompts.` | 60 / 60 (41 gallery + 19 sandbox) | 60 passed |
| Sandbox scenarios and reference guard alone | `FullyQualifiedName~CanDoItAll.Tests.Components.Prompts.PromptsSandboxTests` | 19 / 19 | 19 passed |
| Downstream consumers | `FullyQualifiedName~CanDoItAll.Tests.Components.AgentFramework.LlmChatConversationWorkspaceTests\|FullyQualifiedName~CanDoItAll.Tests.Components.AgentFramework.WorkflowExecutorCanvasCatalogTests` | 12 / 12 | 12 passed |
| Production browser lane (real Web host, PostgreSQL) | `FullyQualifiedName~PromptGalleryBrowserTests` (Playwright solution) | 2 / 2 | 2 passed; four captures written to the git-ignored Playwright output folder (prompt-gallery) |

Production projects built: `CanDoItAll.Modules.Prompts.Contracts`, `CanDoItAll.Prompts.UI`,
`CanDoItAll.Modules.Prompts`, `CanDoItAll.Prompts.UiSandbox`, `CanDoItAll.Web`.

Static gates: portability-static ran on the complete tree without `--tracked-only`; the
19 `ADDED` and 8 `STALE` findings were the relocated compatibility file, the removed module
components, the ordinal-ignore-case comparisons in the new session/form/sandbox code and the
standard README command blocks. The baseline was refreshed once for those reviewed deltas
(94 insertions, 28 deletions) and the final enforcement without `--write-baseline` reports
`PASS (14681 reviewed executable-source findings unchanged)`; the scan was repeated after the
last source edit with the same result. `Test-Documentation.ps1` passed for 217 maintained
files. The broad Stable gate result is in the execution record.

## Build graph measurement

Same machine, Release, `/m:1`, incremental `dotnet build --no-restore`, one appended Razor
comment as the edit, warm graph (initial warm build of `CanDoItAll.Web` 100.5 s).

| Project built | Before extraction (edit in the module) | After extraction (edit in the rendering library) |
|---|---|---|
| Rendering library `CanDoItAll.Prompts.UI` | not applicable | no-op 1.8 s, after edit 2.1 s |
| Sandbox `CanDoItAll.Prompts.UiSandbox` | not applicable | no-op 2.1 s, after edit 2.1 s |
| Module `CanDoItAll.Modules.Prompts` | no-op 4.3 s, after edit 4.4 s | no-op 3.6 s, after edit 4.0 s |
| Web host `CanDoItAll.Web` | no-op 24.0 s, after edit 22.8 s | no-op 18.7 s, after edit 18.7 s |

The smallest graph that recompiles a Prompt Gallery Razor edit is now the 2 s rendering
library or sandbox instead of the 4 s module inside the 19 to 24 s Web graph. The Web
timings are dominated by graph evaluation and vary between runs by a few seconds, so they
are not claimed as an improvement. `dotnet watch` edit-to-visible latency was not measured.

## Readiness

| Dimension | State | Evidence |
|---|---|---|
| Semantic state ownership | Proven | Sessions own filters/page/target/draft identity; hosts mirror parameters and echo only through typed commits; tests above |
| Deterministic rendering | Proven | Surfaces render from explicit presentation records in bUnit and in the sandbox without any service |
| Scenario interactions | Proven | Sandbox scenarios listed in its README; `PromptsSandboxTests` |
| Lightweight compile graph | Proven | Contracts + SharedKernel + BaseLib/Common + Components.Web; guard tests |
| Browser sandbox | Proven | Sandbox served at 1600×1000 with Parity assets in the desktop app browser: normal, editor-edit, picker, compatibility, compatibility-blocked, editor-missing, long-data at 960 px and editor-new inline validation rendered with the production theme and no console errors |
| Production bookmarkability | Deferred | Only the pre-existing `promptId` query is preserved; no filter or pagination URL contract was added |

## Open items

- `PromptGallerySelection` and the picker/composer effect owners remain in the module; a
  later child may move the picker button into a consumer-facing UI family if a second
  consumer archetype needs it.
- The Curator launcher port stays in the contracts assembly with its Agents implementation;
  the page still depends on `NotificationService` for Curator failures.
- Chat and Workflow consumers still take the module assembly transitively; that is the
  existing module dependency direction, not a rendering dependency.

## Execution record (2026-09-16)

- Start: `effeb17c011b86290193ec99e37d406642455480` on `components-decoupling`, clean tree.
  Work is left uncommitted on that branch; nothing was pushed, merged, rebased or reset.
- Production builds: contracts, rendering library, module, sandbox (Parity) and `CanDoItAll.Web`
  each built with 0 errors after the split; the Web build proves the AgentFramework consumers
  (`AgentChatPanel`, `PromptGalleryLlmChatComposerActionContributor`, `WorkflowCanvasEditor`)
  compile unchanged.
- Focused slices and static gates: see the tables above; every discovery count matched the
  stated expectation before execution.
- Browser evidence: production `/prompt-gallery` through the real host and PostgreSQL
  (create → inline validation → save → finalize → close → filter preserved → reopen; missing
  `promptId` → failed editor → close), plus the sandbox scenarios listed under Readiness.
  Observed in both: the desktop filter rail's nine-column template squeezes the two checkbox
  labels at 1600 px and overflows a 960 px container; this is the unchanged baseline template
  and is recorded, not fixed. The sandbox follows the browser color scheme because it has no
  application theme host; production forces its own theme.
- Broad Stable gate (trigger: `CanDoItAll.slnx` gained three projects and the Components
  test project references the new sandbox), run once on the final source with the
  documented commands and filter, Release, `/m:1`: product solution restore and build
  exit 0; Stable test solution restore and build exit 0; filtered test run exit 1.

  | Assembly | Result |
  |---|---|
  | `CanDoItAll.Tests.Components` | 2076 passed, 0 failed (30 m 13 s) |
  | `CanDoItAll.Tests.Integration` | 3009 passed, 0 failed (2 h 47 m) |
  | `CanDoItAll.AgentFramework.Memory.Tests` | 22 passed, 0 failed |
  | `CanDoItAll.Memory.Tests` | 203 passed, 0 failed |
  | `CanDoItAll.Tests.Unit` | 8697 passed, 1 failed (3 m 11 s) |

  The single failure is `CanDoItAll.Tests.Unit.Infrastructure.SecretScanningTests.Repository_contains_no_realistic_provider_keys`.
  It scans the working tree including git-ignored files and reports fixture key patterns in
  `MafImageGenerationResultDisclosureIntegrationTests.cs` copies under
  `artifacts/modules-decoupling/drafts/**` (ten paths, all inside the retained, git-ignored
  module-decoupling evidence that this task must not delete). No reported path belongs to
  this change, and the tracked repository is not affected; the gate is therefore not green
  on this workstation and is reported as such rather than as a pass.
