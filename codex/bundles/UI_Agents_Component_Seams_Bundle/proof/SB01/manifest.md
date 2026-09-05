# SB01 baseline and characterization

Execution baseline: 68db2ee0e63a2ce6baa681e9722acc0a67877b21, components-decoupling. Product source and existing tests match the reviewed a249d77 baseline. Components c3e6aa03a878994c0ba8aed6af017d0be75f3796 and FileTools 7c7453c6583365ae5bd63f8fc6efc4a776e15818 are clean live-source siblings. SDK 10.0.303, Windows, production build Release; browser WatchRun Debug.

## Executed gates

- Production AgentFramework build: exit 0, zero warnings/errors (transcripts/production-build.log).
- Unit discovery and baseline: 13 passed, zero skipped: ten AgentFrameworkSimpleChatsRouteTests cases plus Request_history_context_has_no_invented_summary_or_inherited_agent_selection, Agents_builder_exposes_only_selections_relevant_to_the_active_view, Agents_builder_uses_validated_component_selection_labels. See unit-discovery.log and unit-baseline.log.
- Component discovery and baseline: 49 passed, zero skipped: primary 46, two History_hosts_make_no_aggregate_or_history_reads_until_requested cases (providers/request-history), and Agents_shell_exposes_workflows_page_navigation. See component-discovery.log and component-baseline.log. Discovered method/data names are the baseline inventory, not a final count quota.
- AgentEditorLoadCharacterizationTests: four passed, zero skipped. See characterization-discovery.log and characterization-results.log. Tests use public forms/buttons and a public interface recording proxy around normal production construction.

Core agent/capability load failure currently exposes an editable blank model. Owner scope decision for fixing this existing defect is pending; SB04 must preserve it unless authorized. Clear currently discards identity/version without a write. An acknowledged save followed by catalog refresh failure currently reports generic save failure, gives no completion callback, and leaves a new draft without identity. The latter probe proves UI ordering with an acknowledged fake write; it does not claim a real database commit. B16/B18 safeguards must bind identity and distinguish committed outcomes.

## Host and dependency observations

Overview and New technical agent opened at 1600x1000 through the real /agents host at http://127.0.0.1:5500; initial database-profile Continue used existing development configuration. No editor save/delete or provider request was triggered. Inspected screenshots/overview.png and screenshots/new-agent.png: overview cards/chart/stat layout intact; all ten editor tabs fit, Identity first, internal scroll and fixed Clear/Save footer visible. Native dialog has a Close control. No temporary timing source edits were made.

Evaluated direct AgentFramework references: 46, net10.0 (evaluated-agentframework-references.json). Watch list contains 3,789 output lines including 570 Razor/2,814 C#/112 CSS entries (transcripts/watch-file-list.log). This is a list baseline, not a measured speed improvement. CodeAnalytics snap-20260904231957-7bf47433 covers 308 documents across three projects, not the whole solution. Reported existing AgentFramework/Hosting module cycle and nested image-provider type cycle are outside this change. Components MCP transport closed; live sibling inspection supplies bounded component API evidence.

Managed watch app_a6c762b5cc154efaad35ca3bdb4bc802 reached runtime generation CanDoItAll.Web:1:g0 and served the page, but WatchReady timed out: backend simultaneously reported Healthy/WaitingForChanges and isReadyForHotReload=false. Warm edit-latency samples cannot be trusted and remain outstanding; no zero-duration or improvement claim. Owned session stopped before structural edits. Cold managed startup included build and tool latency, so it is not presented as a cold host benchmark.

## Progression and remaining evidence

SB02 route/lazy/context baseline is green and named. Watch latency remains an explicit B30/SB07 measurement gap, independent of SB02 read/state ownership. B05–B11/B13–B15/B21–B27 current anchors are the named baseline component cases; owning phases must add the missing adversarial/lifecycle/real-child cases from requirements/02-behavior-preservation-matrix.md. B16/B18 load/reset ordering is characterized above; B17/B19/B20 require new session/command proof. No row is closed by inventory alone. B28 browser baseline exists; final interactions remain required.
