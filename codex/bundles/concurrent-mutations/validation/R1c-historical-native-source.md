# Historical receipt review: native source checkpoint

Status: authored and source-reviewed only. No build, discovery, execution, browser,
database or cross-process restart proof was run for this native slice. The R1c
notification-fixture repair is a separate transfer and is not included here.

The native host consumes the distinct `ReadSavedEffectReceiptsAsync` owner boundary.
It never calls recovery, dispatch, approval or journal checkpoint APIs to perform
this read. `AgentChatSession` captures the original selected run's ID, Agent, chat
and creation witness from the validated workspace response before ordinary detail
loading. An ordinary detail failure can therefore leave an explicit historical
read action available. A mismatched returned run clears that reference. Neither
selection capture nor the UI asserts authority: the real owner loads and validates
the original admission and current read policy.

`AgentChatPanel.HistoricalReview.cs` owns one linked read and a separate view/profile
fence. Close/disposal or target refresh retires that read. Its late result/error
cannot update a successor. The host checks the returned exact request and current
local generation, and requires a paged snapshot response to keep its submitted
cursor's full context and run/journal revisions; the canonical service boundary additionally checks the full
server-side profile/fingerprint/scope. It rejects a nonadvancing LookupLimit cursor.
Every Next intent carries the cursor rendered with its button, and the host compares
that request to the current page before reading. A stale callback never substitutes
the newer page's cursor. ContextChanged requires refreshing the native view.

The renderer receives immutable sanitized values and origin-bound intents. It uses
existing Alert, Stack, TextBlock and Button contracts without CSS, new packages,
injected services or persistence dependencies. It replaces each visible page rather
than aggregating observations across witnesses. Accepted historical observations
remain visibly separate from the existing continuation and checkpoint presentation.
They cannot clear either file-lease release uncertainty or activity-finalization
uncertainty, turn a failed run into Waiting, or enable a new effect. The retained
request/page is bounded by the shared four-observation / 16-KiB contract; it contains
no accepted payload or profile fingerprint. Component MCP tools were unavailable;
this review used actual shared component source and existing callsites.

## Source-derived test inventory

`AgentChatPanelResponsivenessTests.HistoricalReview.cs` adds exactly **two Facts**, no
theories, providers, method-level categories or skips. Both belong to the existing
`CanDoItAll.Tests.Components.AgentFramework.AgentChatPanelResponsivenessTests` class,
whose prior reviewed 48 runtime rows become 50. These are planned discovery/runtime
counts, not runner output.

| Fact | Authored arrangements |
|---|---|
| `Saved_receipt_review_retains_original_selection_after_detail_failure_and_pages_only_on_explicit_intent` | Ordinary detail fails, original creation/selection witness survives, zero automatic receipt calls, explicit four-observation page followed by one replacement observation, malformed page context/revision rejected without replacing the old page/cursor, exact captured cursor, stale Next ignored, SnapshotChanged clears pages, ContextChanged disables reading, composer retained, zero send/recovery/approval calls. |
| `Saved_receipt_review_preserves_uncertainty_and_discards_late_results_outside_its_current_view` | Existing unconfirmed checkpoint plus real typed lease/activity failure presentation; explicit accepted historical page leaves exact continuation object, both warnings, failed state and blocked dispatch intact. Separate controlled reads finish after thread replacement, generation change or disposal; cancellation is observed for retired/disposed targets, pending callbacks are drained, no late observations or new writes appear. |

The tests use the existing public orchestrator boundary and existing native host
fixture conventions, with test-only derived proxies. The detail failure is a
controlled adapter failure, not proof of an actual old-generation owner rejection.
The peer's immutable result constructor also rejects mismatched page witnesses;
those arrangements prove preservation after that contract failure, while the
additional native response guard is independently source-reviewed.
Actual generation-nonzero host A to generation-zero host B correspondence, current
owner authorization, namespace/lifetime refusal, cursor witness races and HTTP wire
compatibility remain the peer's owner/integration proof. These component tests must
not be cited as a real process restart or actual receipt lookup.

The narrow selection is the two fully qualified Fact names above joined by `|` with
`FullyQualifiedName=` clauses. A broader native regression selection is the existing
responsiveness class (50 rows), plus any unchanged activity class selected by the
parent's independent source manifest. Do not silently reuse the older 48/57 totals.

## Rendering and pending proof

`HistoricalReceiptSpecimen.razor` adds `/historical-receipts` in the existing Agents
UI sandbox. It uses the actual recovery renderer and explicit owned sample actions:
four mixed observations, a next page, saved-snapshot change and denied read. Reset
does not clear the independent sample lease/activity warnings. This is a source
specimen, not product persistence proof. Its eventual desktop visual check should
verify both warnings remain after an Accepted page, next replaces the first page,
changed/denied pages disclose no observations, and layout has no horizontal overflow.
No sandbox/browser execution has occurred in this slice.

`reviews/R1c-historical-native-rendering-entries.json` contains only the five reviewed
register entries: two changed hosts/renderers and three new host/renderer/specimen
entries. Upsert those exact identities into the newer main register; do not copy the
older managed register wholesale. The source inventory records predecessor hashes
for every preexisting production input and excludes the shared notification fixture.

Parent owns standard Release builds, exact discovery/execution, sandbox visual proof,
rendering-register enforcement and the full no-write portability gate. Source syntax
parsing and scoped diff inspection do not replace those gates. R1d/R2 dispatch
re-admission and any lease-release recovery command remain separate work.
