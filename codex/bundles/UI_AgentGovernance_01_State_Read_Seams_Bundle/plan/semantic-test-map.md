# Exact future witnesses and semantic coverage

**Planned, not implemented or executed.** Proposed class: `CanDoItAll.Tests.Components.AgentFramework.AgentGovernanceReadLifecycleTests`. Each row below is one intended `[Fact]` with the exact method suffix shown. Prefer Facts for explicit sequences. G00 must compile the actual tests, list discovered FQNs and freeze arguments before running; this proposed inventory does not substitute for compiled discovery. Correct names/maps together if source evidence requires a different public seam. Never call a setup error RED.

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
