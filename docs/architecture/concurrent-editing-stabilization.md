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
| R6 Workspace, Security, Plugins, Memory, Collaboration | Workspace defaults, secret metadata, Provider History and Collaboration passed the checks below. The remaining configuration forms and command overlaps are listed by owner. |
| R7 File editors and remaining mutable surfaces | The production Project Structure text editor passed review and conditional Save in two browser circuits. The app-wide writer census remains open beyond the mapped file paths. |

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
| WS01 | Workspace Settings V1 and V2 share a conditional writer. The editor compares original, draft, and saved values. Currency and culture are reviewed together, and policy choices cover the complete decision policy. Applying choices keeps the same form and requires a separate Save. |
| WS02–WS03 | Storage Save requires the observed configuration timestamp. If the draft changes default purposes, the transaction checks the observed routing set and commits both changes together. The focused PostgreSQL cases cover rollback, foreign route insertion, and a competing deletion. Chromium and Firefox verified health-only Test, stale Save, retained local text, and explicit reopen. Typed configuration and routing review and the placement recovery overlap checks remain open. |
| WS04–WS05 | Data Source configuration and reviewed deletion compare the original profile revision under the file owner's lock. Agent catalog transfer compares the reviewed source and target, and replacement of a populated target requires explicit approval. Typed profile comparison and the lifecycle overlap checks for activation, schema work, and restore remain open. |
| WS06–WS07 | API accounts use the original file version and a separate authentication revision. Token issue and revocation use narrow commands and an acknowledgement ledger. Account metadata comparison and the full token lifecycle overlap checks remain open. |
| WS08 | Provider History previews and updates require the observed policy version. The editor compares capture and retention in typed groups, retains the original form, and clears its previous preview after a conflict. Shortening existing history after review requires a new preview and confirmation. |
| WS09 | Dynamic renderer resolution checks the registered owner, trust level, and schema. The cross-session schema change and unknown field round-trip check remains open. |
| SE01 | Secret metadata Save and deletion compare the observed version. A stale operation retains the draft for safe metadata review. The dialog hides private note and JSON contents and asks what to do with a pending replacement. A known accepted rotation clears the submitted replacement while retaining newer input. |
| PL01–PL02 | Grant replacements compare their original scope token. Connection replacements compare their original token and invalidate in-flight authorization on a connection settings change. Callback exchange claims a durable session before dispatch. The focused tests cover the saved grant, stale connections, and a pending callback after disconnect. Typed permission and connection review, live provider refresh, and package lifecycle overlap remain open. |
| ME01–ME02 | Provider replacement uses the original configuration revision across UI, API, and persistence. Runtime operations keep their existing identities. Typed transport comparison and the query and retention overlap checks remain open. |
| CO01–CO02 | Thread creation and reply submission have stable IDs. The owner compares an observed message count within the serialized thread write. A stale Mark read is refused, and an independent stale reply preserves newer unread messages. Inbox, threads, and escalations each load up to 50 rows with server-side filtering and separate aggregate counts. The exact selected thread remains addressable outside the page. Unsent replies stay with their thread, including across filter and tab changes. Leaving the workspace with a retained draft requires explicit discard. Thread titles and state have no separate replacement form. |

## File editors and census

| Production file path | Writer disposition |
| --- | --- |
| Project Structure expanded text preview | The only production FileInteraction Save callback uses the original authorized file session and a SHA-256 conditional write. The edit handle admits View and Edit. Complete reads check their bytes against the content revision. After a conflict, the editor reads the saved text through that handle, shows it beside the draft, and applies its observed revision locally. A separate Save still checks that revision. |
| Project Files, Process Run Files, Resources, and the Project Structure file browser | All four production callers acquire read-only known-file sessions. They supply no FileInteraction Save callback. |
| Project Structure generated image completion | The existing completion processor writes a new media object and attaches it through the original project, placeholder and binding observations. It has its own storage receipt and recovery path. |

Chromium and Firefox verified that a second peer save makes the reviewed revision stale,
that a fresh comparison preserves the draft, and that the separate Save can be read back
from the other circuit. The file close guard also kept the draft until explicit discard.
The FileTools source revision `34be867a51944ea51b30a4adca0d941158418ea0` is published,
and the Windows workflow passed for it. [FileTools CI](https://github.com/fyziktom/CanDoItAll.FileTools/actions/runs/42136867704)

The earlier bundle scanner checked 4,762 source files and found 1,457 heuristic candidates.
The CodeAnalytics snapshot covered Infrastructure, Workspace, and FileTools integration.
The production call-site review above closes the known-file session consumers. The
application-wide disposition of the other census candidates is still open.

## Earlier Agent and chat checkpoint

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

The earlier checkpoint pinned Components `61836966459316a42a0e0a46a70d475c02808fd7`
and FileTools `80d28eaed0baf287c572b9f2439da4d9789279a6`. The Windows Debug Web build
and both Release solution builds passed, as did portability baseline enforcement,
rendering review and exact path checks for 1,344 project and solution references.

The earlier R4 release verified the served dialog, notification, input, and canvas
assets. A Chromium check opened and cancelled a settings comparison against the
ordinary database.

## Earlier Windows validation and port 5032

The current Windows Debug and Release Web builds passed. The main UI component lane
passed 68 cases. The focused renderer and unit checks included 38 Test Lab renderer
cases, 33 Test Lab unit cases, 42 Storage renderer cases, and the Scheduler renderer
and dependency checks. Six native HTTP cases passed for Project Structure polling and
SSE, including replay and rollback. The final affected reruns passed the Agent catalog
transfer, authorized file content, storage routing, and native Windows file-open cases.
The portability baseline passed final enforcement with 15,792 reviewed findings.
Rendering and documentation checks passed. Hosted CI still needs a result for this tip.

The ordinary instance restarted from the Windows Release build on 5032. The runtime
reported ready, Test Lab returned HTTP 200, and the original database selection and
configuration fingerprints matched. Browser writes used the isolated fixture, whose
Web process, browser sessions, PostgreSQL container, runtime directory, and native probe
were released after validation. Execution policy blocked removal of duplicate local
scan files and one test diagnostic. The local resource record lists the retained files.

## R6 and R7 continuation validation

The Windows source builds use the exact signed Components and FileTools revisions pinned
in CI. The default source build was the deployment target for this checkpoint. A package
deployment needs a FileTools package containing the reviewed conflict handling.

The final Windows selection passed 157 main-repository cases across Workspace, Collaboration,
the host renderers, authorized file reads, and native PostgreSQL owners. The sibling FileTools
component selection passed 12 cases and its hosted Windows workflow passed. Main-repository
hosted CI remains pending for the local signed commits.

Chromium 149 and Firefox 140 ran six checks through the real Blazor UI on an isolated
PostgreSQL 18 fixture. They verified Workspace defaults, secret metadata, Provider History,
Collaboration draft retention and paging, and Project Structure file review. The browsers
reported no page errors. The temporary Web process, browser sessions, PostgreSQL container,
and runtime directory were released.

Portability enforcement passed with 15,803 reviewed findings after reviewing 21 added and
10 retired fingerprints. The rendering register passed with 1,267 current inputs. All
1,348 literal in-repository project and solution references matched their tracked paths.

The rebuilt Windows Release is running on port 5032. The runtime reported ready,
Test Lab returned HTTP 200, and the database selection and configuration fingerprints
matched the instance before the restart.
