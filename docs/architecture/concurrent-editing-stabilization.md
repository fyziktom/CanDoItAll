# Concurrent editing stabilization

This checkpoint records the verified changes and the remaining writer review. The
[original rollout plan](../../codex/bundles/CanDoItAll_ConcurrentEditing/operations/02_ROLLOUT.md)
defines the scope.

| Scope | Disposition |
| --- | --- |
| Shared foundation and Prompt Gallery | The approved pilot established conditional writes, scoped change hints, typed comparisons, and native dialog and notification behavior. |
| R1 CRM / HR, R2 Projects and Workbench, R3 Workflow and Process authoring | Passed their recorded owner, editor, and browser checkpoints. See the [execution record](concurrent-editing-prompt-pilot.md). Project Structure retains its existing polling and SSE contracts and explicit graph refresh. |
| R4 Agents, teams, capabilities, providers, Simple Chats, and Agent settings | The scoped owner and UI checkpoints pass. Configuration edits retain the original version and authority; the editor compares local values with saved information and commits only on a separate Save. Floating Agent chats retain each unsent composer through focus changes, hiding, and idle expiry. Stopping an active chat explicitly discards only that chat's local draft. |
| R5 Scheduler, Resources, TestLab | Scheduler and Test Lab require the revision captured at opening. Scheduler groups recurrence and target/input for review. Test Lab compares fields and stable case, evidence, and run rows and preserves independently appended results. Applying choices updates the local form. Save is a separate action. Resource metadata continues to use the native revision and binding protocol. |
| R6 Workspace, Security, Plugins, Memory, Collaboration | The owner changes and remaining proof are listed below. |
| R7 File editors and remaining mutable surfaces | Project Structure file content has byte-based revisions. Remaining mutable surfaces still need individual writer dispositions. |

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
The main Test Lab UI also passed a Chromium and Firefox check. The stale Save kept the
local draft, applying the comparison left the saved plan alone, and the next Save
retained both the reviewed local fields and the other circuit's new case.

## Workspace and configuration owners

| Surface | Verified behavior and remaining proof |
| --- | --- |
| WS01, WS06–WS09 | Workspace Settings V1 and V2 share a conditional writer. API accounts use the original file version and a separate authentication revision. Token issue and revocation use narrow commands and an acknowledgement ledger. Provider history uses a policy version and separate retention confirmation. Dynamic renderer resolution checks its registered owner, trust level, and schema. These paths retain their native contracts. |
| WS02–WS03 | Storage Save requires the observed configuration timestamp. If the draft changes default purposes, the transaction also checks the observed routing set and commits both changes together. The focused PostgreSQL cases cover rollback, foreign route insertion, and a competing deletion. Chromium and Firefox verified health-only Test, stale Save, retained local text, and explicit reopen. Placement recovery uses its existing exact operation and target identities. |
| WS04–WS05 | Data Source configuration and reviewed deletion compare the original profile revision under the file owner's lock. Agent catalog transfer compares the reviewed source and target, and replacement of a populated target requires explicit approval. Activation, schema work, and restore still need the full lifecycle overlap proof in the bundle. |
| SE01 | Secret metadata Save and deletion compare the observed version. The editor reads safe metadata and an explicit new value requests rotation. The existing vault stays the owner of secret material. |
| PL01–PL02 | Grant replacements compare their original scope token. Connection replacements compare their original token and invalidate in-flight authorization on a connection settings change. Callback exchange claims a durable session before dispatch. The focused tests cover the saved grant, stale connections, and a pending callback after disconnect. Live provider refresh and package lifecycle overlap remain outside this checkpoint. |
| ME01–ME02 | Provider replacement uses the original configuration revision across UI, API, and persistence. Runtime operations keep their existing identities. The query and retention overlap matrix remains open. |
| CO01–CO02 | Thread creation and reply submission have stable IDs, duplicate submissions reuse the original effect, and unread changes serialize by thread. Drafts stay with their original selection. The full navigation-discard and bounded inbox review remains open. |

Several R6 forms refuse stale saves and retain their drafts for review. Typed comparison
is present in Scheduler and Test Lab. Wider R6 comparison coverage still needs its own
editor and owner proof.

## File editors and census

Project Structure's editable known-file session uses the authorized storage save target
and a SHA-256 content revision. The accepted receipt describes the submitted bytes.
Project Files, Process Run Files, and the Project Structure browser preview use read-only
known-file sessions. Chromium and Firefox edited the same text asset. The stale save
retained the local text and the authorized readback matched the accepted bytes. The
shared editor exposes a revisionless retry, which the Project Structure edit handle refuses.

The bundle scanner checked 4,762 source files and found 1,457 heuristic candidates.
The CodeAnalytics snapshot covered Infrastructure, Workspace, and the FileTools
integration. The report is a discovery aid. An application-wide disposition of the
remaining candidates is still open.

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
