# Concurrent mutations resumption

The user resumed end-to-end implementation on 2026-10-18. MAIN starts clean at
`5e156af55`; the SPA checkout starts clean at `70c65dd` on
`codex/concurrent-mutations`. SPA changes must remain on that branch. Do not merge
them into SPA main; consultation with the user's colleague is a separate human step.
Native UI work remains Blazor. The SPA checkpoint is API transport/recovery work.

The existing bundle remains compatible: inputs, requirement ledger, plan, phase cards,
reviews and validation records retain their current roles. No scaffold migration is
needed. The October 17 handoff is historical evidence, not a current instruction to stop.
Source-only drafts remain unapproved for integration until their recorded gaps are closed.

## Active entry gate: native chat retention browser proof

Entry: **Pass** for a Behavioral, test-only repair. R1/R6 require original user drafts
to survive concurrent activity without applying an acknowledgement to another context.
The accepted 706-case source is unchanged at entry. The two held source hashes match
their ownership record; both failed attempts remain preserved.

The native panel, submission retention service, activity ownership and disposal paths
remain real. Only `IAgentChatExecutionOrchestrator` is controlled to produce delayed
refusal and an unknown outcome without calling a model or tool. The repair must use the
actual agent-switch dialog after tab navigation, wait for the selected fixture agent,
then select its thread. Changing private UI state or injecting a missing thread would
not prove the intended path. Capture the open switch dialog and all four outcome states
at 1920x1080. Inspect readability, overlay layering, visible actions and overflow.

Selection: Playwright project, exact Fact
`CanDoItAll.Tests.Playwright.AgentChatSubmissionBrowserTests.Native_chat_retains_original_submissions_across_typing_navigation_and_unconfirmed_outcomes`;
expected discovery **1**. Positive/negative coverage: delayed refusal preserves newer
typing, restore does not resend, disposal does not apply to another thread, an unknown
outcome blocks resend/restore across navigation, and no run/message was created. The
test drains the actual submitted panel consumer before disposing the host.

Invalidation keys: the two browser test files, native Agent chat panel/session/retention
and switcher contracts, activity service, test host/bootstrap, project/dependency mode.
No unfiltered suite is justified by this local test-only change. Refresh Release,
run exact discovery/execution, full portability-static and documentation/rendering
checks. This does not rerun or broaden the accepted 706 cases.

Reuse the exact owned PostgreSQL 18 fixture after verifying ID/label/ports and retaining
its baseline. Keep ordinary port 5032, historical unowned TEMP deltas and earlier rejected
cleanup targets untouched. Existing bounded command and observation helpers are reused.
The Components connector is unavailable in this session; the existing switch-dialog
source and existing browser consumers establish the unchanged component contract.

The first resumed browser run discovered one case and failed at the newer draft on the
other thread (32.63 seconds). Navigation through the real switcher succeeded. The
source/runtime freeze verified unchanged; all observed child processes exited and the
fixture database names returned to baseline. Six shared TEMP deltas lack ownership and
were preserved. Evidence remains under `c4-chat-browser-reentry-validation`.

The next test-only repair waits for the textarea's server-rendered `value` attribute
after input and blur before releasing the controlled failure. A browser fill completing
does not acknowledge a Blazor Server event. The post-failure value assertions remain;
no native component or retention behavior is changed. Expected discovery remains one.
That attempt failed before sending: Blazor sets the textarea DOM property and does not
retain a `value` attribute. The failure, unchanged identities and resource reconciliation
are preserved in `c4-chat-browser-ack-validation` (no observed process/database residue;
seven unowned TEMP deltas retained). The corrected check waits for Send to become
enabled on the other thread after input. That visible state requires the server to
accept the nonempty draft, before the original submission is released.
The readiness attempt also failed at the post-refusal value assertion. Source inspection
then showed Send's enabled state does not depend on draft text, so that attempted
acknowledgement was invalid. `c4-chat-browser-ready-validation` preserves the failure
and unchanged identities; no observed process/database residue remains, and eight
unowned TEMP deltas are retained. The successor uses the existing native component
capture to read the selected panel's draft on its renderer, bounded to 45 seconds,
before releasing each controlled outcome. It never sets component state. All original
browser post-outcome assertions remain.
The renderer attempt timed out waiting for the other-thread draft before releasing
the original response. Its final source/runtime check passed and its observed child
processes/databases returned to baseline (eight unowned TEMP deltas retained). A bounded
diagnostic run now records the real panel, workspace and textarea values to locate the
input callback loss. This is investigation, not acceptance; final build/static/browser
proof follows the actual repair.
The diagnostic identified an invalid selection barrier: the default thread was already
selected, so the post-click `aria-pressed=true` assertion was true before the click was
handled. That redundant click replaced the composer after the test filled it. Captured
native panel/workspace/textarea values confirm no input reached the owner. The helper
now skips clicking an already-selected thread and retains the renderer acknowledgement
for subsequent typing. Exploratory component capture was removed. No production change
was necessary; the diagnostic's seven unowned TEMP deltas remain preserved.

## Native chat proof accepted

`c4-chat-browser-selection-validation` discovered and passed **1/1**, in 28.71 seconds.
Three controlled submissions produced four outcome observations, no extra sends, no
run/message persistence and no page errors. Native Web/panel/retention/dialogs remain
real; the execution orchestrator is the controlled boundary. This does not prove model
execution, full-reload retention or the remaining runtime-policy implementation.

The standard Release browser build passed. Full-source portability scan included the
untracked tests; enforcement passed with 15,525 reviewed findings unchanged and no
baseline refresh. Rendering/documentation/diff checks passed. Tool self-tests passed
in the first resumed candidate and their sources were unchanged. The frozen source
contains 8,961 inputs, SHA256
`26E80D882966C541D6038A5A91AC164877700900ECDE5BE3A056E7D7C9F1F89E`;
the 3,763 runtime inputs have SHA256
`008AC7FC49E6A72EC21555C4E430116BE101831937EF944D23EF1F2457E5298F`.
Final verification confirmed both unchanged before documentation closure edits.

All six 1920x1080 screenshots were inspected: original prompt/target and recovery
actions are readable, selected threads are distinguishable and the switch dialog
layers correctly. Horizontal-overflow assertions pass. Scrolling is needed to show the
composer with the retained banner; transient notifications overlap its area in immediate
captures and are dismissible. The test proves actual input/restore/send state through
the native controls; these screenshots do not qualify all responsive layouts.

Observed child processes exited and PostgreSQL database names returned to the exact
baseline. Seven new shared TEMP entries have no ownership attribution and are preserved
in `final-resource-reconciliation.json`. Earlier cleanup refusals remain untouched;
complete historical resource closure is not claimed. The same isolated PostgreSQL
fixture stays available for subsequent lanes. SPA remains clean on its own branch.
Reviewed evidence is retained in [the browser proof](../proof/2026-10-18-chat-browser/evidence.json),
with its command results, identity verification, resource reconciliation and screenshots.

## Continuing implementation

After this proof closes, continue the handoff's dependency order: coherent P03 lifecycle
owner/caller/migration, actual Agent/Process/Workflow policy execution, remaining owners,
external effects, performance repairs and final broad validation. Do not treat persisted
policy editing as runtime enforcement or activate the incomplete cancellation/planning
drafts. Keep incremental commits and update this record with each validated result.
