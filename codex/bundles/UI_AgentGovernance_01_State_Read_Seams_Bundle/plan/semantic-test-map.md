# Exact future witnesses and semantic coverage

**G00 current-panel witnesses compiled; execution receipts retained under proof/G00.** Proposed class: `CanDoItAll.Tests.Components.AgentFramework.AgentGovernanceReadLifecycleTests`. Each row below is one intended `[Fact]` with the exact method suffix shown. Prefer Facts for explicit sequences. G00 must compile the actual tests, list discovered FQNs and freeze arguments before running; this proposed inventory does not substitute for compiled discovery. Correct names/maps together if source evidence requires a different public seam. Never call a setup error RED.

| Exact future method | Expected current result from source | Semantic expectation |
|---|---|---|
| Requested_agent_A_list_late_success_cannot_replace_B | Characterization candidate | Preserve existing agent-generation acceptance guard. |
| Requested_agent_A_late_failure_cannot_fault_B | RED candidate | Old list/automatic-detail exception cannot publish Failed or escape into current target. |
| Catalog_completion_cannot_overwrite_a_newer_agent_request | RED candidate | Catalog/request capture and callback are current-owned. |
| Run_R1_detail_late_success_cannot_replace_R2 | RED candidate | R2 retains detail and identity. |
| Run_R1_detail_late_failure_cannot_fault_R2 | RED candidate | Old exception cannot fault the current UI. |
| Replacing_agent_cancels_pending_list_read | RED candidate | Backend receives canceled owner token. |
| Replacing_run_cancels_pending_detail_read | RED candidate | New run owns detail request. |
| Disposing_panel_cancels_pending_catalog_read | RED candidate | Catalog token canceled, no callback. |
| Disposing_panel_cancels_pending_list_and_detail_reads | RED candidate | All active owner reads canceled; late results ignored. |
| Noncooperative_completion_after_disposal_publishes_nothing | RED candidate | Success/failure/finally after removal is inert. |
| Old_detail_finally_cannot_clear_newer_busy_state | RED candidate | New pending request remains visibly loading. |
| Refresh_detail_cannot_override_newer_manual_run_selection | RED candidate | Manual R2 survives refresh R1 completion. |
| Accepted_list_remains_visible_when_automatic_detail_fails | RED candidate | List success accepted; independent detail error. |
| Missing_requested_agent_does_not_fall_back_to_all_agents | Characterization candidate | Local missing-ID guard remains fail-closed. |
| Missing_agent_retry_keeps_target_through_real_page_echo | Unproven composition candidate | Page null callback/echo cannot erase requested failure identity. |
| Same_preferred_agent_echo_preserves_manual_run_selection | Characterization candidate | Repeated parameter echo does not restart or reset. |
| Explicit_all_agents_then_same_requested_agent_reloads_target | Unproven composition candidate | Explicit null is All; return to A resolves A again. |
| Stale_callbacks_cannot_publish_A_or_failed_access_after_B | RED candidate | Captured current selection and access only. |
| Detail_failure_keeps_validated_agent_context_ready | RED candidate / documented product decision | Context reflects valid agent, separate detail availability. |
| Wrong_run_identity_in_detail_is_rejected | RED candidate | No wrong-run content accepted. |
| Wrong_agent_identity_in_detail_is_rejected | RED candidate | Match accepted row AgentId even in All agents. |
| Same_target_failed_refresh_retains_explicit_stale_list | RED candidate for presentation | Accepted list survives honestly marked stale. |
| Different_agent_loading_hides_previous_agent_rows | RED candidate | No A rows beneath B selector. |
| Removed_selected_run_remains_explicitly_unavailable | RED candidate / documented product decision | Refresh does not silently choose a different row. |
| Retry_detail_does_not_reload_catalog_or_run_list | RED candidate | Only failed detail lane restarts. |
| Initial_read_failure_shows_bounded_error_and_retry | RED candidate | Raw exception text absent; no fake empty success. |
| Synchronous_reads_and_repeated_echo_do_not_duplicate_loads | Characterization/guard candidate | Install request slot before async invocation. |

These are independent behavior claims, not a requirement for a permanent number of tests. Variants may be expanded when actual public execution reveals distinct causes. Freeze exact final discovery and retain both initial setup failures and corrected semantic results.

G01 adds `CanDoItAll.Tests.Unit.AgentFramework.AgentGovernanceSessionTests` for public lane transitions/immutable copy and no-replay retries, including successful same-target reuse and canceled read distinction. New registered integration class: `CanDoItAll.Tests.Integration.AgentFramework.AgentGovernanceReadsTests`, with exact planned facts:

- `Registered_reads_filter_agent_and_preserve_canonical_order_and_take`
- `Registered_detail_reads_preserve_run_identity_and_governance_sections`
- `Registered_read_cancellation_propagates_without_execution_effects`
- `Missing_canonical_run_remains_unavailable_without_fallback_selection`

G02 class `CanDoItAll.Tests.Components.AgentFramework.AgentGovernanceSurfaceTests` must freeze exact compiled cases for real section rendering, controlled intents, safe/escaped errors, immutable collections, badge/summary compatibility and no-service rendering before execution. It does not exist yet; G00 cannot claim a new surface compilation failure as current-panel RED.

G03 freezes exact sandbox query/render tests after specimen design and current runtime-dialog consumer discovery; no filename, partial count or test-count architecture assertions. Browser/read races are proven through public fixtures and rendered input/button events, never private-field reflection.

Existing owner inventory is [separate](../inventory/existing-tests.json). Preserve Overview `Aggregate_failure_cannot_override_ready_selection_access(tab: Governance)` and the actual page/context suite; that callback test alone is not a Governance verification lifecycle proof.

G00 added exact methods: Empty_guid_is_invalid_and_never_publishes_all_agents (Fact); Canceled_owner_allows_delayed_token_registration_without_late_publication (false/true); Governance_timestamps_are_explicit_UTC_independent_of_culture_and_offset (en-US/+120 and cs-CZ/-240); Forbidden_execution_payloads_and_raw_runtime_prose_never_render (Fact); Runtime_details_shared_children_use_UTC_and_omit_raw_timeline_message (Fact). Initial exact discovery: 34 cases. This count describes this checkpoint, not architecture. Timestamp/payload/shared-child rendering cases remain owned through G02; all current-panel lifetime cases must pass G01. Optional timestamps and DST pairs gain direct public value-policy tests in G01/G02.

## G01 implementation witnesses frozen before execution

`AgentGovernanceSessionTests` contains 14 cases: List_is_accepted_before_detail_completes;
Refresh_cannot_repeat_detail_after_newer_manual_selection_during_catalog;
Older_catalog_continuation_cannot_start_a_list_for_newer_request;
Older_list_finally_cannot_clear_newer_refresh; List_retry_does_not_repeat_catalog_or_context_effects;
Removed_run_can_reappear_without_changing_selection; Accepted_list_owns_its_collection;
Canceled_current_read_surfaces_failure_when_owner_did_not_cancel; and
Disposal_cancels_each_lane_and_fences_noncooperative_completion (Catalog/List/Detail × success/failure).
These supplement the unchanged 34-case G00 selection. Discovery must verify this inventory;
the case count is an execution expectation, not an architecture invariant.

`GovernancePresentationTests` freezes 16 direct cases before execution: immutable public
contract allowlist; denied domain fields; collection independence; bounded rows with
retained totals; Unicode truncation; four UTC/culture/offset combinations; missing timestamp;
and six relative/absolute artifact path examples. The Unit selection is exactly these two
classes (30 cases). No raw runtime exception or payload string is an accepted UI summary.

`AgentGovernanceSurfaceTests` freezes ten in-place cases: service-free complete sections;
controlled selection intents; three lane retry intents; encoded HTML-like labels;
usable list during detail loading; removed target; denied sentinel; bounded labels and
accessible current selection. G00 initially reached 32/34; the two failures exposed
incorrect retry-button DOM attributes. Production attributes were corrected, with no
G00 assertion changes. The next Components selection contains G00 plus Surface (44 cases).

The second in-place execution passed all 34 unchanged G00 cases and all 30 Unit cases.
Two new Surface assertions accidentally selected the list heading. They now target the
explicit detail heading, and the long-label assertion also requires the truncation marker.
No G00 test required correction. The four previously named registered-read integration
cases are now implemented with real application registration and PostgreSQL test profiles;
canonical execution data is stored by the production profile-scoped file execution store.

## Accepted context revision follow-up

Frozen before execution: `AgentGovernanceContextRevisionTests.Returning_to_valid_agent_after_missing_request_republishes_accepted_context` (1 case). An accepted target must be republished after an intervening failed target; no missing target is published as All agents.

## Sandbox cases frozen before execution

GovernanceSandboxTests: Representative_sample_uses_only_allowlisted_bounded_values (14 scenarios), Controlled_selection_changes_sample_and_records_typed_intent, Detail_retry_preserves_sample_list_and_selected_target, Missing_sample_target_does_not_become_all_agents_on_retry. Expected discovery: 17 cases. Counts describe this execution inventory, not architecture invariants.

## Affected existing consumers

Frozen owning selection: AgentsHomePageTests (6), AgentsOverviewReadLifecycleTests (24), AgentChatModalTests.Runtime_details_dialog_* (2), AgentFrameworkModuleChatContextBuilderTests (11). Exact filtered compiled discovery is checked before execution. These cover the real page, accepted context, retained Overview behavior, and both runtime-details consumers.

Frozen additional context case: `AgentGovernanceContextRevisionTests.Accepted_new_route_target_resolves_parent_loading_access`. The actual AgentsWorkspaceState.ApplyRoute resets access for B; accepting cached B must restore Ready through the host callback. Context class discovery is now 2 cases.

Browser failing-first: the explicit requested agent option exists after circuit attachment, but the native select displays All agents. Preserve option identity while replacing the pending target placeholder with the loaded catalog. The browser checks native value as well as accepted rows; bUnit alone does not model native select behavior.


## Final visual and documentation adjudication

The browser fixture additionally freezes `Refresh text must fit inside its button` as a DOM range predicate. It failed on the extracted header, then passed in Web, Parity and Fast after the description moved below the title/action row. No Components layout change was made. Existing G00 assertions remain unchanged. The browser fixture project now has its required README; the documentation gate has only its 118 inherited tracked-log paths remaining.
