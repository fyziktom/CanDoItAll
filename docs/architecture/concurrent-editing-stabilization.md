# Concurrent editing stabilization

This checkpoint freezes the approved rollout through the main Agent and Simple Chat
editors. The [original rollout plan](../../codex/bundles/CanDoItAll_ConcurrentEditing/operations/02_ROLLOUT.md)
remains the scope record. This checkpoint does not assert concurrency coverage across
all application writers.

| Scope | Disposition |
| --- | --- |
| Shared foundation and Prompt Gallery | The approved pilot established conditional writes, scoped change hints, typed comparisons, and native dialog and notification behavior. |
| R1 CRM / HR, R2 Projects and Workbench, R3 Workflow and Process authoring | Passed their recorded owner, editor, and browser checkpoints. See the [execution record](concurrent-editing-prompt-pilot.md). Project Structure retains its existing polling and SSE contracts and explicit graph refresh. |
| R4 Agents, teams, capabilities, providers, Simple Chats, and Agent settings | The scoped owner and UI checkpoints pass. Configuration edits retain the original version and authority; the editor compares local values with saved information and commits only on a separate Save. Floating Agent chats retain each unsent composer through focus changes, hiding, and idle expiry. Stopping an active chat explicitly discards only that chat's local draft. |
| R5 Scheduler, Resources, TestLab | Scheduler and Test Lab require the revision captured at opening. Scheduler groups recurrence and target/input for review. Test Lab compares fields and stable case, evidence, and run rows and preserves independently appended results. Applying choices updates the local form. Save is a separate action. Resource metadata continues to use the native revision and binding protocol. |
| R6 Workspace, Security, Plugins, Memory, Collaboration | Pending. Includes administrative Agent catalog transfer, which still has a separate replacement path. |
| R7 File editors and remaining mutable surfaces | Pending application-wide writer census and closure. |

The Gantt tab now uses the Project Structure update inbox and the same bounded refresh
acknowledgement as Canvas. Selecting an update in Gantt refreshes the schedule and
highlights its surviving tasks. Chromium 149 and Firefox 140 ran separate circuits on
an isolated PostgreSQL fixture. A task creation and inline title edits delivered notices
in both directions. The checks covered local edit suppression, in-place refresh, removal
of refreshed rows, one window across tab switches, and clearance below the Gantt toolbar.
The Windows Debug Web build and ten Gantt renderer cases passed.

Scheduler and Test Lab read plans in bounded pages. The existing plan-list filters apply
to the displayed page. Scheduler's agent search applies its filters before pagination.
The Windows PostgreSQL checkpoint passed 95 of 97 selected cases on its first run. Both
Scheduler source authority cases passed after the service retained its own accepted
revision in the caller's editor model. The Test Lab follow-up passed 33 unit cases and
38 renderer cases. Scheduler and Test Lab reuse the shared comparison renderer. Their
dependency guards allow the editing libraries and continue to reject owner assemblies.

Voice settings share the native workflow settings row and its version. Floating chat
settings use their own original row observation and reject stale replacements even when
a hint is missed. Opening access remains bound to the original actor and dataset. The
voice preview also requires execution access. Reading and applying a comparison stays
local; an unconfirmed save blocks a retry until the user reviews the current saved values.
Undo restores the earlier local draft over the reviewed version. It does not restore a
superseded write token. Live Agent messages retain their existing submission and recovery
protocol; drafts remain circuit state.

The stabilization pass uses Windows builds and focused verification. The current checks
covered 51 UI cases, four Agent chat draft retention cases, 43 native settings persistence
and authority cases, 59 Components cases, and 35 canvas composer JavaScript cases. The
earlier Agent, provider, Simple Chat, and R1–R3 checkpoints remain scoped evidence.

Chromium 149 and Firefox 140 used independent circuits at 1920×1080 against the real
main application and an isolated PostgreSQL database. Six checks covered stale saves,
unfinished numeric input, settings navigation, voice conflicts, floating chat retention,
and definition and title Undo. The form remains the primary surface; comparison panels
own their scrolling and keep the choices and Apply action reachable. The normal settings,
scrolled comparison, and close confirmation captures were inspected. This pass sent no
model request. The detailed logs and images stay in the local ignored proof directory.

CI checks out the signed Components tip `61836966459316a42a0e0a46a70d475c02808fd7`
from the published [Components dependency branch](https://github.com/fyziktom/CanDoItAll.Components/tree/codex/concurrent-editing-components) and the existing FileTools
revision `80d28eaed0baf287c572b9f2439da4d9789279a6`. The pinned revisions match the sources
used by the Windows build. This is a local validation checkpoint; a hosted CI result is
a separate delivery check. The Windows Debug Web build and both Release solution
builds passed, as did portability baseline enforcement, rendering review and exact
path checks for 1,344 project and solution references.

The ordinary instance on port 5032 was restarted from these Release outputs. Its
original database selection and configuration hashes were retained, and the served
dialog, notification, input, and canvas assets match the reviewed sources. A Chromium
check opened and cancelled a settings comparison against the ordinary database without
submitting a write.
