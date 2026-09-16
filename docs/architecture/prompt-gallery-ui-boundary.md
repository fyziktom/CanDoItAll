# Prompt Gallery UI boundary

Maintained record of the Prompt Gallery presentation decoupling on `components-decoupling`:
architectural decisions, the behavior matrix, the validation performed, and open items.
Historical Agents execution records remain in `modules-decoupling`; this document is the
single maintained record for the Prompt Gallery seam, including the hardening pass that
corrected the extracted architecture (see the execution records at the end).

## Ownership

| Responsibility | Owner | Notes |
|---|---|---|
| Rendering of the search list, editor form and compatibility warning | `src/UI/CanDoItAll.Prompts.UI` | Controlled surfaces: explicit presentation records in, typed intents out; no `[Inject]`; scoped CSS moved with the markup; one `EditContext` per editing source shared by Save and Create final version |
| Public Gallery value contracts, enums, `IPromptGalleryService`, Curator ports | `src/Modules/CanDoItAll.Modules.Prompts.Contracts` | Same `CanDoItAll.Modules.Prompts` namespace; only the declaring assembly changed |
| Search requests, debounce, request generations, favorite writes | `PromptGallerySearchSession` + `PromptGallerySearchHost` (module) | One session per host instance; never a scoped service |
| Editing target, loads, draft/version/archive/suppression writes, identity adoption, accepted persisted baseline | `PromptGalleryEditorSession` + `PromptGalleryItemEditorHost` (module) | Reports `PromptGalleryEditorCommit` to its owner instead of mutating its own parameter; keeps the accepted token and content separate from the latest read |
| Picker dialog lifetime, immutable-version selection, nested editor refresh | `PromptGalleryPickerDialog` (module) | Closes only its own `DialogReference` |
| Picker button and chat composer effects, compatibility check, consent, suppression | `PromptGalleryPickerButton`, `PromptGalleryChatComposerButton` (module) | Interaction context = consumer, provider, model and the owner's `TargetKey`; a per-interaction token linked to the lifetime is passed to `DialogService.OpenAsync`; a genuine context transition retires the current interaction (cancels its dialog, releases the ownership slot at once) and fences every late result of the retired chain |
| Route, `promptId` request, editor dialog, catalog invalidation, optional Curator | `PromptGalleryPage` (module) | Curator port unchanged; page lifetime token passed to the launcher; late Curator effects after disposal are suppressed; the lease is released exactly once |
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

Evaluated compile graph of `CanDoItAll.Prompts.UI` after the hardening pass (project
references only, sibling source mode for Components): `CanDoItAll.Modules.Prompts.Contracts`
→ `CanDoItAll.SharedKernel`; `CanDoItAll.Components.BaseLib` → `CanDoItAll.Components.Common`;
`Microsoft.AspNetCore.Components.Web`. No project reference was added or removed by the
hardening pass. The guard test `PromptsUiBoundaryTests` asserts the *direct* referenced-assembly
set of the rendering library, the absence of `[Inject]` on every component, and that public
signatures expose no other assembly; it is not a recursive proof of the transitive graph, which
is stated here from the evaluated project references instead. The sandbox references only the
rendering library; `PromptsSandboxTests` asserts its referenced assemblies exclude the module,
Infrastructure, Entity Framework, Web, AppComponents and AgentFramework.

### Persisted baseline and conflicts

The editor session keeps an *accepted persisted baseline*: the concurrency token and the
normalized editable content (title, summary, kind, phase, content, tags, supported models,
consumers, recommendations) of the revision the draft was loaded from or last written by this
editor. After a successful save the baseline is the receipt token plus the *submitted* content.
Every read-back is reconciled against it:

- a read-back whose header token equals this editor's own receipt refreshes only identity
  metadata (project, collection); it never replaces the submitted baseline content. The owner
  loads the header and each collection with separate statements and no snapshot transaction,
  so under PostgreSQL Read Committed the header token does not prove that the returned
  collections belong to the same revision. Returned content that differs from the submission
  can only come from another actor's revision and raises the conflict alert immediately;
- a read-back whose editable content equals the accepted baseline is adopted with its newer
  token, because only this editor's non-editable commands (archive, restore, finalize)
  intervened;
- any other read-back keeps the old token, so the backend rejects the next save with
  `prompts.gallery.concurrency-conflict`, and the surface shows a conflict alert with an
  explicit **Reload latest and discard my draft** action (the `Retry` intent in the Ready
  phase). Nothing reloads, retries or replays automatically.

Content equality is structural: tags compare as normalized ordered sets, consumers as ordered
sets, and each supported provider/model declaration as a typed key of normalized provider,
normalized model and preference flag. A joined string would let a delimiter inside a provider or
model name (which the owner accepts) make different declarations compare equal.

### Current versus retired interactions

`PromptGalleryPickerButton` and `PromptGalleryChatComposerButton` own at most one *current*
interaction each, keyed by their context (consumer, provider, model, owner `TargetKey`). A
genuine context transition or disposal *retires* that interaction: its token source is
cancelled (which closes its dialog through `DialogService.OpenAsync`) and the ownership slot is
released at once, so the new context can open its picker, evaluate compatibility, ask for
consent and insert while the retired chain is still unwinding. A retired chain keeps its own
token source until it ends, observes its late success, failure or cancellation through the
generation fence (no insertion, notice, dialog close or state change), and clears the slot only
if it still owns it, so it can neither block nor clear a newer interaction. A same-context
rerender changes nothing, and a second admission for the same current context is rejected
while its interaction is open. The picker's click handler admits or rejects synchronously and
runs the interaction as an owned task: the BaseLib `Button` disables itself while its click
task is in flight, so a handler that awaited the whole chain would keep the button itself
blocked for as long as a retired chain stayed pending. Cancellation stays cooperative: a
committed preference write is not rolled back, it is simply never followed by an insertion
for the retired target.

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
| Desktop filter rail: the filter bar spans the list column (an auto-fit rail repeats once under fit-content sizing and collapsed to one column otherwise); below the `xl` breakpoint the controls wrap in an auto-fit grid; at `xl` the checkbox labels and clear action take intrinsic (`max-content`) tracks | fixed nine-column template at every width in a fit-content bar → full-width bar with breakpoint-specific templates | Correction | Playwright `Filter_rail_stays_inside_its_container_at_desktop_and_constrained_widths` (1600×1000 single row without overflow, 1100×900 wrapped without overflow); sandbox `layout=narrow` |
| Editor phases Closed/New/Loading/Ready/Missing-Failed; a missing record never becomes a new draft | `loadError` string → `PromptGalleryEditorPhase` | Preserve/Safeguard | `Missing_item_is_failed_with_its_requested_identity_and_retry_reloads_it`, host `Missing_item_shows_a_failed_state…`, surface `Failed_phase_shows_retry…` |
| Target change A→B during a read invalidates A's effects | unguarded → generation fence | Safeguard | `Target_change_during_load_ignores_the_late_result…` |
| Dispose invalidates reads and presentation effects but does not roll back a committed write | n/a | Safeguard | `Disposal_ignores_a_late_read`, `Late_save_result_after_dispose_is_not_published_and_the_write_is_not_replayed` |
| Same target + normal rerender keeps EditContext, validation and in-progress fields | baseline reloaded on parameter echo after save (`ItemId` self-mutation) → source-reference reset only | Safeguard | surface `Rerender_with_the_same_source_preserves_edits…`, host `Owner_echo_of_the_adopted_identity…` |
| First save identity: baseline set `ItemId`, cleared `loadedItemId`, then reloaded; a failed reload lost the identity | receipt adopted before any refresh | Safeguard | `First_save_adopts_identity_and_token_from_the_receipt_even_when_the_re_read_fails`, host `First_save_then_failed_re_read_then_second_save…` |
| Repeated Save cannot create a second record; duplicate submit while busy is ignored | busy only on the button → session-level command coordination | Safeguard | `Duplicate_submit_while_a_save_is_in_flight_is_ignored`, surface `Busy_presentation_blocks_duplicate_commands` |
| Submission snapshot independent of later edits and nested collections | n/a → `PromptGalleryEditorSubmission` copies | Safeguard | `PromptGalleryEditorFormTests.Submission_snapshot_is_independent…`, surface `Submit_requires_name_and_content_and_emits_an_independent_snapshot` |
| Save draft and Create final version pass the same `EditContext` validation, so an unparseable numeric input blocks both; the corrected input yields exactly one command with the corrected snapshot | finalize bypassed the form's `OnValidSubmit` in the extracted surface | Safeguard | surface `Invalid_numeric_input_blocks_save_and_finalize_until_the_input_is_corrected` (real `InputNumber` change, Save and Finalize) |
| Interaction policy during a command: the whole form is disabled (`fieldset disabled`), not only the button | Correction (policy made explicit) | Correction | surface `Busy_presentation_blocks_duplicate_commands`, sandbox `editor-busy` |
| `ExpectedUpdatedAtUtc` and concurrency conflicts; no automatic overwrite; a rejected save shows the conflict alert | Preserve | Preserve | `Concurrency_conflict_keeps_the_draft_identity_and_token_without_overwriting` |
| Foreign revision between load and an own archive/restore: the read-back carries foreign content, so the accepted token is kept, the next save is rejected, the conflict is visible, and Reload discards the draft and accepts the latest revision | extracted session adopted every read-back token, which would have let the stale draft overwrite the foreign revision | Safeguard | `Own_archive_after_a_foreign_content_change_keeps_the_accepted_token_and_flags_the_conflict`, `Reload_after_a_conflict_discards_the_draft_and_accepts_the_latest_revision`, host `Foreign_change_before_an_own_archive_is_surfaced_as_a_conflict…`, real persistence `Foreign_content_change_before_own_archive_is_not_overwritten_by_the_stale_draft` |
| Foreign revision between an own save receipt and its read-back: the receipt token stays accepted, so the next save is rejected instead of silently adopting the foreign token | same | Safeguard | `Foreign_change_between_the_save_receipt_and_the_read_back_keeps_the_receipt_token`, real persistence `Foreign_change_between_own_receipt_and_read_back_is_rejected_on_the_next_save` |
| Mixed read-back (own header token, another actor's collections, read between the owner's separate statements): the submitted baseline is kept, the conflict is shown at once, a later own archive does not adopt the foreign collections, and the stale save is rejected | own-token read-back replaced the baseline content → submitted content kept | Safeguard | `Own_token_read_back_with_foreign_collections_keeps_the_submitted_baseline_and_the_stale_save_is_rejected` (models, tags, consumers), `Own_token_read_back_with_matching_collections_keeps_the_next_own_command_conflict_free`, PostgreSQL `PostgreSql_MixedReadBackAfterOwnReceipt_DoesNotAuthorizeOverwritingAnotherEditorsModelSet` (statement-level barrier between the header and the supported-model read) |
| Supported provider/model declarations compare structurally; a delimiter inside a provider or model name cannot make two different declarations equal, while reordering, repetition and case/whitespace normalization stay equivalent and a changed preference flag stays different | pipe-joined string key → typed key | Safeguard | `PromptGalleryPersistedContentTests` (6 facts), session `External_model_pair_change_that_only_differs_in_delimiter_placement_is_not_adopted_by_an_own_archive` |
| A retired interaction (cancelled by a target transition) releases its ownership slot immediately: the new target opens its picker, evaluates compatibility, consents and inserts while the retired chain is still pending; the retired chain's late success, failure or pending preference write inserts nothing, notifies nothing and leaves the new target's dialog and state untouched; A→B→A admits a fresh interaction for A; a same-context duplicate admission is still rejected | slot cleared only by the old task's `finally` → released at retirement | Safeguard | composer `New_target_inserts_while_the_retired_compatibility_request_is_still_pending_and_its_late_outcome_is_inert`, `Retired_completion_neither_closes_nor_decides_the_new_target_warning_dialog`, `Pending_preference_write_of_a_retired_target_neither_blocks_the_new_target_nor_inserts_late`, `Context_A_B_A_admits_a_fresh_interaction_for_A_while_the_retired_A_work_stays_obsolete`, `Same_context_rerender_keeps_the_current_interaction_and_a_duplicate_admission_is_rejected` |
| The picker's interaction identity is the owner's `TargetKey` when one is supplied; a provider or model that resolves for that target after the picker opened (the workflow canvas loads its provider options asynchronously) is a parameter update and keeps the open picker; without a `TargetKey` the provider and model remain the identity. The composer keeps provider and model in its own identity because its compatibility check depends on them | the hardening pass treated any provider/model change as a transition, which closed the canvas picker while its provider options settled and broke `WorkflowsPageTests` | Safeguard (regression fix) | composer `Provider_and_model_resolving_after_the_picker_opened_keep_the_same_target_picker_open`, `WorkflowsPageTests` (the three canvas facts that select a Gallery prompt) |
| Own archive, restore, finalize and save in sequence without a foreign change never conflict: each newer token is adopted because the editable content still matches the accepted baseline | Safeguard | Safeguard | `Own_finalize_and_archive_without_a_foreign_change_adopt_each_newer_token`, `Archive_toggle_commits_and_re_reads_the_token_for_the_next_save`, real persistence `Own_archive_restore_and_finalize_keep_the_next_save_valid_without_a_foreign_change` |
| "Create final version" = save draft, then create version; "draft saved, finalization failed" is distinct from "nothing saved"; busy spans the whole command; the catalog is told about the committed draft | baseline busy ended after `SaveCore` | Safeguard | `Finalize_reports_draft_saved_when_version_creation_fails`, `Finalize_success_commits_draft_then_version…`, host `Finalize_partial_success…` |
| Mutation outcome is separated from owner refresh/notification failures: a `Committed` callback that throws after a valid receipt keeps the saved identity and token and is reported as "Editor update failed", never as a failed save or version; a returned version failure and a thrown version request stay distinct outcomes | one catch reported every exception as the command's failure | Safeguard | `Committed_callback_failure_after_a_valid_receipt_keeps_the_saved_identity_and_reports_only_the_effect`, `Owner_callback_failure_after_a_successful_version_keeps_the_version_known`, `Version_result_failure_and_thrown_version_request_are_distinct_outcomes` |
| Command input is frozen before the awaited busy publication; a target change re-entered during that publication cancels the command before any write, and a target transition during a suspended commit callback issues no further write to the old target | n/a | Safeguard | `Reentrant_target_change_during_the_busy_publication_cancels_the_command_before_any_write`, `Target_transition_during_a_suspended_commit_callback_issues_no_further_write` |
| Unknown persistence outcome: keep the draft, no identity guess, no blind replay, explicit warning | generic catch → warning state | Safeguard | `Unknown_save_outcome_keeps_the_draft_without_an_identity_and_warns_before_any_retry` |
| Warning suppression persisted immediately, independent of Save draft | Preserve | Preserve | `Warning_suppression_is_persisted_immediately…` |
| Picker: item with a final version inserts the exact immutable snapshot; missing/failed version never falls back to the draft | Preserve | Preserve | `Selection_with_a_final_version_inserts_the_immutable_snapshot`, `Missing_final_version_does_not_substitute_the_draft`, `Draft_only_item_inserts_the_draft_content` |
| Picker closes only itself; EditRequested override closes first, then invokes | `DialogService.CloseAsync()` (last dialog) → cascaded `DialogReference` | Safeguard | `Edit_requested_override_closes_picker_before_invoking_callback`, `Nested_editor_commit_refreshes_the_picker_list_and_leaves_unrelated_dialogs_open` |
| Nested editor commit refreshes the picker's own list in place | remount-free `RefreshAsync` | Preserve | same |
| Dialog ownership tied to the real `DialogService.OpenAsync` lifetime; disposing the owner closes only its dialogs | no token in baseline → per-interaction token linked to the lifetime | Safeguard | `Disposing_the_picker_button_closes_only_its_own_dialog…`, composer `Disposing_the_composer_closes_its_warning_dialog_and_never_inserts` |
| The whole picker → details read → compatibility → consent → suppression → insertion interaction is bound to its originating context (consumer, provider, model, owner `TargetKey`); a genuine transition closes the picker or warning dialog and drops every late result; A→B→A stays obsolete; a same-context rerender keeps the open dialog; provider/model equality is not target equality | provider/model only, dialog left open on change | Safeguard | composer `Context_change_while_the_real_picker_is_open_closes_it_without_a_selection`, `Context_change_while_the_selection_details_read_is_pending_drops_the_selection`, `Context_change_while_compatibility_is_pending_ignores_its_late_success_or_failure`, `Context_A_B_A_keeps_the_original_interaction_obsolete`, `Same_context_rerender_keeps_the_open_picker_and_the_selection_is_inserted`, `Provider_or_model_change_while_the_warning_is_open_closes_it_and_drops_the_selection` |
| Compatibility check before insertion; blocking error offers only Cancel and is enforced by the host whatever decision a dialog or callback path supplies; Insert anyway / Insert and suppress semantics; nothing inserted without consent; the chat picker's search is scoped to the chat consumer, so a workflow-only item is never listed there | Preserve, host enforcement added | Safeguard | composer `Incompatible_selection_opens_dialog_and_cancel_does_not_emit_content`, `Blocking_compatibility_ignores_a_forced_insert_decision`, `Warning_dialog_cancel_inserts_nothing_and_writes_no_preference`, Playwright `Embedded_chat_picker_inserts_a_compatible_item_and_asks_before_inserting_a_model_restricted_item` (compatible insert, workflow-only item not listed, provider/model warning with Cancel and Insert anyway against the real host) |
| A rejected or thrown optional suppression write is reported as "Warning preference was not saved", the consented insertion happens exactly once, nothing is replayed, and the remaining preferences are still attempted; a target change during the write inserts nothing | rejected write already tolerated; thrown write was reported as an insertion failure | Safeguard | composer `Rejected_or_thrown_preference_write_is_reported_separately_and_the_consented_insertion_happens_once`, `Partial_preference_outcome_across_multiple_issues_inserts_once_without_replay`, `Context_change_during_the_preference_write_inserts_nothing` |
| `/prompt-gallery` route and `promptId` query; A→null→A and A→missing→A reopen; the same value echoed by a rerender does not reopen a closed editor | baseline acknowledgement never cleared, so A→null→A did not reopen | Safeguard | `PromptId_request_reopens_after_null_and_after_a_missing_item_but_not_on_an_echo`, Playwright `Missing_promptId_request_shows_a_failed_editor_and_a_valid_request_reopens_the_persisted_item` |
| Catalog invalidation after an editor commit | `@key` remount (reset filters and page) → `RefreshAsync` (preserves filters and page, clamps to the last page) | Correction | `Editor_commit_refreshes_the_list_in_place_and_preserves_filters`, `Refresh_reloads_the_current_page_and_clamps…` |
| Curator optional, context activated/synchronized, presentation retry, functional UI without Curator; a slow presentation read never blocks the gallery or the `promptId` request | Preserve; activation failure no longer throws out of the page | Safeguard | `PromptGalleryPageTests` Curator facts, `Slow_curator_presentation_blocks_neither_the_gallery_nor_the_promptId_request`, `Open_failure_while_alive_is_reported_and_the_next_click_retries` |
| Curator lifecycle: page lifetime token passed to the launcher; late presentation or open results after disposal (success, failure or honoured cancellation) publish no notice and no render; the context lease is released exactly once through the real renderer dispose | no token, no disposed checks | Safeguard | `Late_presentation_outcome_after_disposal_is_suppressed_and_the_lease_is_released_once`, `Late_open_outcome_after_disposal_publishes_nothing` |
| Required name/content before saving | toast (`NotificationService.Warning`) → inline validation alert in the surface; backend validation results still arrive as toasts | Correction | surface `Submit_requires_name_and_content…` |
| "Add model" provider/model required and duplicate detection | toast → inline message | Correction | `PromptGalleryEditorFormTests.Supported_models_are_deduplicated…` |

Explicitly recorded corrections: in-place refresh instead of remount (filters/page survive),
inline required-field and add-model validation instead of toasts, the whole-form busy policy,
and the responsive filter rail. No delete, schema, routing, or URL contract was added.

## Consumers

- `PromptGalleryPage` (`/prompt-gallery`) renders `PromptGallerySearchHost` and, inside the
  declarative BaseLib `Dialog`, `PromptGalleryItemEditorHost`.
- `PromptGalleryPickerDialog` renders `PromptGallerySearchHost` in compact mode and opens
  `PromptGalleryItemEditorHost` as a nested dialog when no `EditRequested` override exists.
- `AgentChatPanel` passes its chat session generation as `TargetKey` to
  `PromptGalleryChatComposerButton`; `LlmChatComposerActionContext` gained an optional
  `TargetKey` (default `null`, so existing contributors compile unchanged) which
  `LlmChatConversationWorkspace` fills with its selection generation and
  `PromptGalleryLlmChatComposerActionContributor` forwards; `WorkflowCanvasEditor` passes the
  node identity (or the `new-component` marker) as `TargetKey` to `PromptGalleryPickerButton`.
  These are the only consumer source changes; the selection contract `PromptGallerySelection`
  is unchanged.
- The sandbox renders `PromptGallerySearchSurface`, `PromptGalleryItemEditorSurface` and
  `PromptCompatibilityWarningSurface` directly.

## Validation

Commands run from the repository root in Release with `/m:1`. Discovery counts were stated
before execution. The table shows the reconciliation and interaction pass; the counts of the
earlier passes are kept in their execution records below.

| Slice | Filter | Expected / discovered | Result |
|---|---|---|---|
| Editor session reconciliation, real InMemory persistence owner and baseline comparator | `FullyQualifiedName~CanDoItAll.Tests.Unit.Prompts.PromptGalleryEditorSession\|FullyQualifiedName~CanDoItAll.Tests.Unit.Prompts.PromptGalleryPersistedContentTests` | 41 / 41 (27 session + 3 persistence + 5 reconciliation + 6 comparator) | 41 passed |
| Composer and picker ownership | `FullyQualifiedName~CanDoItAll.Tests.Components.Prompts.PromptGalleryChatComposerButtonTests\|FullyQualifiedName~CanDoItAll.Tests.Components.Prompts.PromptGalleryPickerDialogTests` | 30 / 30 (24 composer + 6 picker) | 30 passed (the six liveness cases failed against the reviewed source) |
| PostgreSQL mixed read-back (Integration solution, local PostgreSQL) | `FullyQualifiedName~PromptGalleryMixedReadBackPersistenceIntegrationTests` | 1 / 1 | 1 passed |
| Bounded Gallery slices | `FullyQualifiedName~CanDoItAll.Tests.Unit.Prompts.` and `FullyQualifiedName~CanDoItAll.Tests.Components.Prompts.` | 58 / 58 and 90 / 90 | 58 passed and 90 passed |
| Production browser lane (real Web host, PostgreSQL) | `FullyQualifiedName~PromptGalleryBrowserTests` (Playwright solution) | 4 / 4 | 4 passed; twelve captures written to the git-ignored Playwright output folder (prompt-gallery) |

Persistence evidence by provider: `PromptGalleryEditorSessionPersistenceTests` (3 facts) run the
real `PromptsService` on EF InMemory and interpose only before a whole read; they prove the
owner's application logic, normalization and token check, not statement interleaving.
`PromptGalleryMixedReadBackPersistenceIntegrationTests` runs the real service on PostgreSQL
with a `DbCommandInterceptor` barrier that holds A's read-back between the artifact header
statement and the supported-model statement while B commits through a separate context; it
is the only relational proof of the mixed read-back path.

Production projects built for this pass: `CanDoItAll.Modules.Prompts` and its consumers
through the test solutions (Unit, Components, Integration, Playwright), each with 0 errors.

Static gates: see the execution records; each pass ran portability-static on the complete tree
without `--tracked-only`, finished with the no-write enforcement, and ran
`Test-Documentation.ps1` after updating this record.

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
The hardening pass changed no project reference, so the measurement was not repeated.

## Readiness

| Dimension | State | Evidence |
|---|---|---|
| Semantic state ownership | Proven | Sessions own filters/page/target/draft identity and the accepted persisted baseline; hosts mirror parameters and echo only through typed commits; picker/composer own their interaction context; tests above |
| Deterministic rendering | Proven | Surfaces render from explicit presentation records in bUnit and in the sandbox without any service |
| Scenario interactions | Proven | Sandbox scenarios listed in its README; `PromptsSandboxTests` |
| Lightweight compile graph | Proven for the direct reference set, stated for the transitive graph | Direct references guarded by `PromptsUiBoundaryTests`; the transitive graph (Contracts → SharedKernel, BaseLib → Common, Components.Web) is the evaluated project-reference graph recorded above, not a recursive test assertion |
| Browser sandbox | Proven | Sandbox served with Parity assets in the desktop app browser: normal, editor-edit, picker, compatibility, compatibility-blocked, editor-missing, long-data and editor-new inline validation with no console errors; after the rail correction the `layout=narrow` constrained container wraps the rail without overflow |
| Production browser | Proven for the covered paths | Real host and PostgreSQL: create/validate/save/finalize/reopen, missing and valid `promptId`, embedded chat picker insertion with a selected agent, consumer-scoped listing (workflow-only item absent), provider/model warning with Cancel and Insert anyway, rail at 1600×1000 and 1100×900 |
| Production bookmarkability | Deferred | Only the pre-existing `promptId` query is preserved; no filter or pagination URL contract was added |

## Open items

- `PromptGallerySelection` and the picker/composer effect owners remain in the module; a
  later child may move the picker button into a consumer-facing UI family if a second
  consumer archetype needs it.
- The Curator launcher port stays in the contracts assembly with its Agents implementation;
  the page still depends on `NotificationService` for Curator failures.
- Chat and Workflow consumers still take the module assembly transitively; that is the
  existing module dependency direction, not a rendering dependency.
- The blocking compatibility path (`ConsumerNotSupported`, `Archived`) is proven by component
  tests and the sandbox only: the production chat picker lists items through a search scoped
  to the chat consumer without archived items, so such an item is never offered there and the
  host-side enforcement is a defense against a stale listing or a future callback path, not a
  reachable click sequence. The production lane proves the provider/model warning path.
- The Curator open path in production still requires the Agents runtime; only the page-side
  lifecycle is covered by component tests.

## Execution record: extraction (2026-09-16)

- Start: `effeb17c011b86290193ec99e37d406642455480` on `components-decoupling`, clean tree.
  The extraction was committed by the operator as `402f615eedc8bdf245cb87a18123096afa7f6d40`
  ("prompt gallery UI refactor"); nothing was pushed, merged, rebased or reset.
- Production builds: contracts, rendering library, module, sandbox (Parity) and `CanDoItAll.Web`
  each built with 0 errors after the split; the Web build proves the AgentFramework consumers
  (`AgentChatPanel`, `PromptGalleryLlmChatComposerActionContributor`, `WorkflowCanvasEditor`)
  compiled unchanged at that time.
- Focused slices at extraction time (Release, `/m:1`, counts stated before execution): Unit
  `FullyQualifiedName~CanDoItAll.Tests.Unit.Prompts.` 34 / 34 passed; Components
  `FullyQualifiedName~CanDoItAll.Tests.Components.Prompts.` 60 / 60 passed (41 gallery + 19
  sandbox); sandbox alone 19 / 19; downstream `LlmChatConversationWorkspaceTests` and
  `WorkflowExecutorCanvasCatalogTests` 12 / 12; Playwright `PromptGalleryBrowserTests` 2 / 2
  with four captures.
- Static gates at extraction time: portability-static on the complete tree without
  `--tracked-only`; the 19 `ADDED` and 8 `STALE` findings were the relocated compatibility
  file, the removed module components, the ordinal-ignore-case comparisons in the new
  session/form/sandbox code and the standard README command blocks; the baseline was refreshed
  once for those reviewed deltas (94 insertions, 28 deletions) and the final enforcement
  without `--write-baseline` reported `PASS (14681 reviewed executable-source findings
  unchanged)`. `Test-Documentation.ps1` passed for 217 maintained files.
- Browser evidence at extraction time: production `/prompt-gallery` through the real host and
  PostgreSQL (create → inline validation → save → finalize → close → filter preserved →
  reopen; missing `promptId` → failed editor → close), plus the sandbox scenarios listed
  under Readiness. Observed then: the desktop filter rail's nine-column template squeezed the
  two checkbox labels at 1600 px and overflowed a 960 px container; this was recorded as the
  unchanged baseline template and is corrected by the hardening pass below. The sandbox
  follows the browser color scheme because it has no application theme host; production
  forces its own theme.
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

## Execution record: hardening pass (2026-09-16)

- Start: `402f615eedc8bdf245cb87a18123096afa7f6d40` on `components-decoupling`, clean tree.
  The hardening work is left uncommitted on that branch; nothing was pushed, merged, rebased
  or reset, and no signing or permission configuration was touched.
- Preserved: the contracts assembly, the rendering library, the session/host split, the
  sandbox, the intents/presentation records and every behavior classified Preserve above.
- Corrected (source): `PromptGalleryEditorSession` (accepted persisted baseline, conflict
  alert, stage-tracked outcomes, safe effect wrappers, frozen command input, ownership
  re-checks), `PromptGalleryEditorPresentation` (`ExternalChange`),
  `PromptGalleryItemEditorSurface` (shared `EditContext`, `ValidationMessage` under the
  numeric inputs, conflict alert with Reload), `PromptGallerySearchSurface` (full-width filter
  bar, breakpoint templates and intrinsic tracks, `data-item-id` on the Details action for
  browser evidence),
  `PromptGalleryPickerButton` and `PromptGalleryChatComposerButton` (interaction context with
  `TargetKey`, per-interaction tokens, host-enforced blocking, isolated suppression writes),
  `PromptGalleryPage` (lifetime token, disposal fencing, exactly-once lease release), and the
  `TargetKey` plumbing in `LlmChatComposerActionContext`, `LlmChatConversationWorkspace`,
  `PromptGalleryLlmChatComposerActionContributor`, `AgentChatPanel` and `WorkflowCanvasEditor`.
- Tests executed (Release, `/m:1`, counts stated before execution and matched): Unit
  `FullyQualifiedName~CanDoItAll.Tests.Unit.Prompts.` 47 / 47 passed (13 search + 27 editor +
  4 form + 3 persistence); Components `FullyQualifiedName~CanDoItAll.Tests.Components.Prompts.`
  83 / 83 passed (64 gallery + 19 sandbox); downstream
  `LlmChatConversationWorkspaceTests|WorkflowExecutorCanvasCatalogTests|AgentChatPanel`
  72 / 72 passed; Playwright `PromptGalleryBrowserTests` 4 / 4 passed with twelve captures in
  the git-ignored Playwright output folder. The scripted gallery fake gained a persisted-details
  builder that mirrors a submission so read-backs of own writes can be scripted exactly; the
  three persistence facts run the real `PromptsService` through `PromptGalleryTestSupport` (EF
  InMemory) and interpose only before a whole read to inject the foreign revision.
- Static gates: the portability tooling self-tests passed (6 + 4); portability-static ran on
  the complete tree without `--tracked-only` (30417 findings, the untracked persistence test
  included) and the enforcement without `--write-baseline` reported
  `PASS (14681 reviewed executable-source findings unchanged)`, so no `ADDED` or `STALE`
  finding existed and the baseline was not touched; `Test-Documentation.ps1` passed for 217
  maintained files after the record update.
- Browser evidence: the Playwright captures listed in the Validation table; the misnamed
  case now reopens a genuinely persisted item through its `promptId`; the embedded chat
  composer, with the first listed agent selected, inserts a compatible item into the real
  composer, never lists a workflow-only item, asks before a model-restricted item (Cancel
  inserts nothing, Insert anyway appends the content once); the rail is measured at
  1600×1000 (one grid row, no overflowing control) and 1100×900 (wrapped, no overflow). The
  in-app browser was used only to diagnose a click-during-rerender race in the new chat case
  (the picker opens and inserts in the real host; the test now opens the picker with a
  bounded retry) and to confirm the sandbox `layout=narrow` rail after the correction.
- Unavailable evidence: no production capture of the blocking compatibility path (see Open
  items, it is not reachable through the chat picker's consumer-scoped listing) and no
  production capture of Insert and suppress (covered by component tests, kept out of the
  browser lane to leave no preference behind); the Stable gate was not rerun because the
  hardening pass changed no solution,
  project set, build configuration or shared fixture, so no named trigger applies; the
  `SecretScanningTests` status recorded above is unchanged and unrelated to this change.
- Deferred: none of the mandatory findings; the open items above remain open by design.

## Execution record: reconciliation and interaction fixes (2026-09-16)

- Start: `c2022c380a67a7fb86798d0c8140446786b49d0e` ("prompt gallery fixes", the hardening
  pass committed by the operator) on `components-decoupling`, clean tree. The review of that
  commit was a static source review; its three findings were reproduced here with the tests
  named in the behavior matrix before the source was corrected. Nothing was pushed, merged,
  rebased or reset; no signing or permission configuration was touched.
- C1 (mixed read-back contaminating the accepted baseline): corrected in
  `PromptGalleryEditorSession.Reconcile`. The own-token branch no longer replaces the baseline
  content with the read-back; it keeps the submitted content, refreshes identity metadata and
  raises the conflict alert when the read-back differs. The owner's loader was left as it is:
  a feature-scoped snapshot transaction would have changed the persistence owner and the
  InMemory persistence tests for a defect that the session can close alone, and the
  PostgreSQL regression proves the session rule against the real statement interleaving.
- C2 (ambiguous provider/model key): corrected by `PromptGalleryModelKey`, a typed key of
  normalized provider, normalized model and preference flag; the comparator keeps order
  independence, distinct-entry semantics and case/trim normalization. The owner's separate
  unit-separator duplicate validation was not touched.
- C3 (retired interactions blocking new targets): the six new liveness cases failed against the
  reviewed source for two reasons. The ownership slot was only cleared by the old task's
  `finally`, as reviewed; and the picker's click handler awaited the whole interaction while
  the BaseLib `Button` disables itself for as long as its click task is in flight, so the
  retired chain also kept the shipped button itself unclickable. Corrected by
  `RetireInteraction()` in both effect owners (the slot is released when the context
  transition cancels the interaction; the retired chain keeps its own token source until it
  ends and clears the slot only if it still owns it) and by a click handler that admits or
  rejects synchronously and runs the interaction as an owned task. No registry of retired
  work; the generation fence is unchanged.
- Validation (Release, `/m:1`, counts stated before execution and matched):
  - Unit `FullyQualifiedName~CanDoItAll.Tests.Unit.Prompts.PromptGalleryEditorSession|FullyQualifiedName~CanDoItAll.Tests.Unit.Prompts.PromptGalleryPersistedContentTests`
    41 / 41 passed (27 session + 3 InMemory persistence + 5 reconciliation + 6 comparator).
  - Components `FullyQualifiedName~CanDoItAll.Tests.Components.Prompts.PromptGalleryChatComposerButtonTests|FullyQualifiedName~CanDoItAll.Tests.Components.Prompts.PromptGalleryPickerDialogTests`
    30 / 30: against the reviewed source the six new liveness cases failed (target B's picker
    never opened while A's chain was pending, 24 passed); after the correction 30 passed.
  - Integration `FullyQualifiedName~PromptGalleryMixedReadBackPersistenceIntegrationTests`
    1 / 1 passed on the local PostgreSQL server (fresh database per run through
    `PostgresTestDatabaseLease`).
  - Bounded slices after the corrections, with the Unit assembly rebuilt for the final source:
    `FullyQualifiedName~CanDoItAll.Tests.Unit.Prompts.` 58 / 58 passed and
    `FullyQualifiedName~CanDoItAll.Tests.Components.Prompts.` 90 / 90 passed.
  - Browser lane `FullyQualifiedName~PromptGalleryBrowserTests` (Playwright solution rebuilt
    for the final source, real Web host and PostgreSQL) 4 / 4 passed; twelve captures in the
    git-ignored Playwright output folder.
- Static gates: the portability tooling self-tests passed (6 + 4); portability-static ran on
  the complete tree without `--tracked-only` after the session edit and again after the
  picker edit; both enforcements without `--write-baseline` reported
  `PASS (14681 reviewed executable-source findings unchanged)` and the baseline was not
  touched. `Test-Documentation.ps1` ran after the final update of this record.
- Not run at the time: the broad Stable gate (no solution, project set, build configuration
  or shared fixture changed; the new Integration test uses the existing PostgreSQL lease). The
  historical `SecretScanningTests` failure on ignored retained artifacts is unchanged and not
  cleared by this pass.
- Regression found later by the CRM / HR Home Stable gate (2026-09-16, same day): the
  downstream filter of this pass did not include `WorkflowsPageTests`, whose three canvas
  facts select a Gallery prompt through the real picker. The canvas resolves the picker's
  provider and model asynchronously after its first render, and this pass treated that
  provider/model change as a target transition, so the freshly opened picker was cancelled
  before the search could run. Corrected by making the owner's `TargetKey` the picker's
  interaction identity (see the matrix row above); the composer's own identity still includes
  provider and model. The corrected slices are recorded in the CRM / HR Home record.
