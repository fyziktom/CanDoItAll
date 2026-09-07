# Frozen planned behavioral map

The original table below is **HISTORICAL PLANNING**. Actual method names, theory expansion, RED classification and GREEN executions are recorded in the child proof and current execution sections below. Each name below is one proposed Fact unless explicit case expansion is stated. Freeze actual compiled discovery before execution. Counts are discovery expectations, not architecture invariants. Current correct cases may pass before changes; retain as characterization and do not fabricate RED.

Prefix C = `CanDoItAll.Tests.Components.AgentFramework.AgentsHomePageReadLifecycleTests.`; U = `CanDoItAll.Tests.Unit.AgentFramework.AgentsOverviewSessionTests.`; S = `CanDoItAll.Tests.Components.AgentFramework.AgentsOverviewSurfaceTests.`; E = `CanDoItAll.Tests.Components.AgentFramework.AgentsOverviewEffectTests.`. Start C witnesses against the current real page/public query seam, so new-class compilation is never the negative proof.

| Topic | Exact planned method (prefix above) | Witness / shallow implementation caught | Owner |
|---|---|---|---|
| T01 rapid A -> B, A last | C`Usage_scope_latest_request_wins_when_earlier_success_arrives_last` | Two deferred reads, B accepted; A cannot replace metrics/scope. | O00/O01 |
| T02 route while active | C`Route_scope_replacement_starts_a_new_read_while_previous_read_is_pending` | Route parameter B during A starts B once; echo does not repeat. | O00/O01 |
| T03 dispose overview | C`Disposing_page_cancels_overview_read_and_ignores_late_completion` | Token canceled; completion causes no render/notification. | O00/O01 |
| T04 dispose usage | C`Disposing_page_cancels_usage_read_and_ignores_late_completion` | Same for usage, separately from overview. | O00/O01 |
| T05 stale failure | C`Earlier_usage_failure_cannot_replace_newer_success_or_clear_newer_busy_state` | Late A failure cannot change accepted B or an active C. | O00/O01 |
| T06 wrong-scope success | U`Wrong_scope_result_is_rejected_without_publishing_its_totals` | Return wrong stamped scope to current read; explicit failure, no fake success. | O01 |
| T07 history demand | C`History_entry_and_reentry_do_not_start_aggregate_reads` | Providers/RequestHistory each: fresh and after cached Overview; no new aggregate. Two theory cases frozen at O00. | O00/O01 |
| T08 HR partial | C`Hr_lookup_failure_preserves_overview_and_usage_data` | Existing registered adapter/failure fixture retains usable aggregate. | O00/O01 |
| T09 overview retry | C`Overview_failure_is_explicit_and_retry_reloads_only_overview` | Failure visible; one retry, no warmup or unrelated reads. | O00/O01 |
| T10 typed partial usage | C`Partial_usage_failure_renders_successful_source_data_and_warning` | Nonfailed contributions/unknown/unpriced totals retained with warning. | O00/O01 |
| T11 demand isolation | C`Usage_scope_change_does_not_reload_header_overview_hr_or_bound_resources` | Per-port counters, real UI scope selection. | O00/O01 |
| T12 accepted dialog scope | E`Usage_dialogs_open_only_for_the_matching_accepted_scope` | Click while B pending cannot open A-as-B; after B accepted each dialog receives B. Three explicit dialog theory cases. | O02 |
| T13 one team intent | S`Team_shortcut_emits_one_typed_navigation_intent` | Real button once -> exact team identity once, no navigation service. | O01/O02 |
| T14 service-free chart/list | S`Real_charts_and_consumer_lists_render_without_feature_services` | Actual figures/summary/list, no workspace/usage/backend registration. | O01 |
| T15 one count owner | C`Header_and_overview_use_the_same_accepted_summary_across_tab_changes` | Distinct changing totals, refresh and tabs; no stale copied header. Source ownership review additionally required. | O01/O02 |

Additional exact direct witnesses:

- U`Overview_and_usage_generations_are_independent` — unrelated refresh cannot cancel/unlock the other lane.
- U`Same_scope_echo_reuses_pending_read_but_explicit_retry_starts_once` — no lost/duplicated read.
- U`Canceled_old_finally_cannot_clear_newer_read_state` — noncooperative completion, exact generation owns busy.
- U`Accepted_snapshot_collections_are_independent_from_query_result` — later source collection changes cannot alter accepted data.
- `CanDoItAll.Tests.Unit.AgentFramework.AgentsOverviewPresentationTests.Chart_series_and_options_are_independent_between_instances` — no shared mutable options/arrays.
- E`Changing_scope_closes_only_owned_usage_dialogs_and_cancels_their_reads` — real DialogHost, unrelated overlay remains.
- E`Disposing_page_and_dialog_suppresses_late_notifications` — actual removal then late success/failure.
- E`Team_navigation_preserves_existing_route_and_occurs_once` — one typed event reaches existing page route builder once, existing push behavior.
- `CanDoItAll.Tests.Integration.AgentFramework.AgentsWorkspaceQueryTests.Registered_split_reads_preserve_history_demand_and_independent_header_failure` — actual registered ports/isolated fixture, no fabricated adapter success.

These names are frozen before implementation but not asserted to exist now. If more cases are needed, record the source reason and revised discovery before running. T01-T05/T09 are intended semantic RED; T06 direct guard becomes executable with the new session after the current-page races are already witnessed. T07/T08/T10/T11 preserve existing correct contracts while extending combinations. Add meaningful RED for any newly discovered change before fixing it. Do not count one tests-exists/status check as behavior proof.

## Current execution additions

[O00 frozen intent](../proof/O00/planned-red.json) and actual compiled discovery under O00 proof add independent context/HR/bound failure, stale same-scope refresh, scope replacement, old-finally and exact-echo behavior. These use the real page and production query with public controlled sources. The new authorization supersedes the historical Surface-in-O01 allocation: state/read cases close O01, controlled rendering/effects/dialog cases close O02.

## O02 executed verification lifecycle

The exact compiled method/theory inventory is retained in proof/O02/raw. AgentsOverviewEffectLifecycleTests covers all three dialog cancellation, noncooperative late success/failure, replacement, wrong snapshot, conflicting source metadata, safe errors, partial data, independent options, old-finally ownership, page removal and same-page query navigation preserving unrelated dialogs. One real team click and repeated detail intent verify one effect. AgentsOverviewSurfaceTests proves service-free real rendering, typed intents, stale/unavailable and mismatch behavior. AgentsOverviewPresentationTests proves immutable copies and read-phase mapping. Final owning execution: 72 Unit, 78 Components, 4 Integration; 14 sibling ownership/approval cases. Counts are execution receipts only.

## O03 executed extraction/sandbox proof

OverviewSandboxContextTests covers explicit specimen/scenario/scope tokens, existing defaults, frozen baseline value equality and mismatched sample behavior. OverviewSandboxTests renders the actual charts without feature services, drives named states and typed scope/retry/team/detail intents, checks replace-history and preserves Catalog/Capabilities specimens. Final compiled discovery and commands are retained in proof/O03/tests; proof/O03/final/owning-selections.json lists exact filters.

Final focused execution is 160 Unit, 199 Components and four Integration cases (363 total), all passed with zero skips. The initial baseline equality assertion compared ImmutableArray backing identity and failed despite equal values; its corrected public value comparison and both executions are retained. No new-class compile failure is represented as semantic RED. Responsive browser RED and corrected pre-owner timing are separate evidence in proof/O03/responsive-reentry.md.
