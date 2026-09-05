# Actual browser proof

Run: SB07 INV-COMPOSITION INV-SESSION INV-WRITE, 2026-09-05 UTC. Browser: 1600 x 1000 desktop, https://localhost:7271. The startup dialog identified the active development database at 127.0.0.1:5432/candoitall_development; no profile was switched. Runtime/session/actual Playwright action results and owned-process shutdown are recorded in browser-final-actions.json.

The final browser used the freshly built Release CanDoItAll.Web.dll through the managed PublishedDll launch mode. This mode name does not imply a publish/deployment: no publish was performed. The managed SourceRun shadow build failed on an existing Windows maximum-path problem under its artifact directory; ordinary solution/stable builds passed. The earlier runtime and initial stable run were invalidated after the browser exposed the first-save duplicate-editor defect.

## Observed workflows

- Created "Seams final proof 2026-09-05 0401", saved with one editor remaining open, changed name and summary, visited all ten sections, saved again with unchanged catalog count and one editor. Clear produced a new blank draft with no Delete action; closing and reopening the saved card recovered the updated persisted summary.
- Opened the real avatar overlay and pressed Escape. Only the nested dialog closed and focus returned to Choose avatar.
- Opened delete confirmation for the exact owned agent, cancelled and retained its editor, then confirmed its deletion. Removed the earlier owned "Seams proof 2026-09-05 0343" fixture as well.
- Created "Seams proof team 2026-09-05 0406", used the real member dialog to assign the existing .NET Application Developer, observed the one-member catalog, renamed the team, then deleted that exact temporary team under its existing confirmation policy.
- Loaded Providers, Request history, Voice, Floating chat, Simple Chat definitions/conversations and Governance using public buttons. Request history retained its unrequested state on opening. The public Workflows action reached /agents/workflows and the settled Workflows heading; returned to Agents.
- Scrolled the real Capabilities editor internally to its bottom: scrollTop 14388, clientHeight 881, scrollHeight 15269. Save remained inside the 1000px viewport, bottom 962.84375. Closed the unsaved editor.
- Final cleanup: zero open dialogs, 29 catalog cards (original count), zero owned proof agents/teams. No provider invocation, capability execution or chat message was sent.

The initial browser pass in browser-initial-actions.json exposed two editor presentations after the first new save, with only one persisted record. The real-page failing-first component test reproduced that defect. The corrected final browser and 130-case focused gate validate the host identity-echo repair. Historical initial results are not presented as final passing evidence.

## Screenshot review

Actual images were visually inspected at readable desktop scale.

| Artifact | Question and finding |
|---|---|
| screenshots/final-saved-editor.png | One editor, legible identity fields, all ten section labels, coherent margins and visible Clear/Save/Delete footer. Existing dialog title remains "New technical agent" during this create-then-save presentation; no title redesign was made. |
| screenshots/final-delete-confirmation.png | Exact temporary agent name and destructive warning are visible; centered topmost confirmation, distinct Cancel/Delete, blurred parent. |
| screenshots/final-editor-scroll.png | Bottom capability cards are readable without horizontal clipping; internal scroll leaves Close and Clear/Save reachable. |
| screenshots/avatar-overlay.png | Real avatar choices, topmost overlay and close controls are visible. Final keyboard focus return is recorded in the corrected action log. |
| screenshots/storage-overlay.png | Real current storage catalog, staged selection and Cancel/Apply actions render within the overlay. Open/cancel was exercised before the final host echo fix; unchanged descendant source is associated through the source manifest. |
| screenshots/capability-overlay.png | Real three-step capability wizard shows identity fields and Cancel/Back/Next. Open/cancel proof; persistence and assignment are covered by actual adapter/component cases, not inferred from this image. |
| screenshots/final-overview.png | Captures the legitimate loading state immediately after navigation, with coherent dashboard spacing and placeholders. It is not evidence of completed data loading; baseline/initial overview and real query/component cases supply that evidence. |
| screenshots/saved-editor.png | Historical failing first-save presentation; retained only as negative evidence. |

No layout/CSS redesign was included. The adjacent workspace checks are mount/navigation smoke checks; they do not claim exhaustive provider/governance/voice feature validation.

## Console and cleanup

transcripts/browser-final-console.log reports 2 total informational messages, **0 errors and 0 warnings**, for the final navigation session before shutdown. Old disconnected-runtime reconnect messages are outside that final navigation log. Browser-final-actions.json includes successful stop of the owned app_5de111995c6549969afd511565ff025b session; the backend terminated its owned PID 30308 (graceful=false). No other runtime or stable test process was stopped.

