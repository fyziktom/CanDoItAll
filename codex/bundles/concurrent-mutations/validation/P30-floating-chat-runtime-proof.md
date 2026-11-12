# P30: floating chat and runtime QA evidence

Status: complete for this repair. Implementation, reported-run recovery, focused tests,
final native browser checks and the ordinary host refresh pass.

Design: [floating chat and runtime proof](../../../../docs/architecture/concurrent-mutations/12-floating-chat-and-runtime-proof.md).
Engine branch: `concurrent-mutations`. Shared resize fix: Components branch
`codex/native-gantt-lifetime`, signed commits `0a944a3c`, `df86f99b`, `0e2d7b9c` and
`306c0e29`. Initial engine checkpoint: signed commit `580ec4428`; the final engine changes
and validation record are committed on the same branch. SPA remains clean at `1139939` on `codex/concurrent-mutations`,
unchanged and unmerged.
CI pins the reviewed, published Components revision `306c0e2988667f94095914c483fd8bcf792ef4c6`.

## Changes and boundaries

Focused agent chat places routine thread badges and saved-receipt review in Conversation
details. Context permissions, affinity and follow/detach actions use a separate controlled
dialog; blocked, detached and pending context remain visible. The compact composer uses a
single wrapping action row. Disclosure preserves the mounted editor and conversation.
Warning/error badges and approval actions stay in the primary surface. When conversation
loading fails, historical receipt review stays reachable beside the visible failure.
Full-page chat retains its previous presentation. Native owners still validate action
origins and perform reads/writes; disclosure itself performs neither.

OverlayLib keeps native resize local until release, ignores recent geometry echoes during
newer gestures, preserves explicit reset/minimize/visibility changes, and removes owned
listeners/timers on disposal. No pointer-move server calls or feature-specific resize
runtime were added. The existing bounded host and state contract remain in place.

Native testing then exposed an existing minimize/restore draft-loss defect: minimization
disposed the whole conversation component. OverlayLib now offers opt-in retained content
while minimized, and the conversation window enables it. Hidden content stays mounted
with its state and subscriptions; ordinary overlays retain their original disposal
behavior. Hide/close and selection ownership remain unchanged. Both shared lifetime
cases pass, including retained state and default disposal.

The same native sequence exposed browser focus scrolling the overflow-hidden outer frame,
clipping its header. The runtime now holds only the frame's offsets at zero; body and
transcript scrolling remain independent. The owned scroll listener is removed on disposal.
Native proof confirms retained drafts and an unclipped frame together.

The reported QA attempt had valid browser checks for its first preview host, followed by
a second start at the same URL for a service-worker update check. Its latest evaluation
receipt did not contain the host URL; earlier navigation preceded that start. The latest
host correlation gate therefore refused completion. The generic browser execution brief
now requires fresh navigation and proof after every start/restart, before the matching
startup receipt is stopped. Cached/offline proof remains supplementary. No application
name, route, business domain or acceptance criterion is embedded in the fix, and no gate
was weakened. Rework was requested through the native Live Processes dialog for only the
blocked QA step, with the same lifecycle instructions; completed upstream work is retained.

That retry passed lifecycle correlation and selected the configured repair branch for a
real generated-app defect: the service worker included itself in integrity-checked cache
assets, and its published hash disagreed with the manifest. Installation failed, leaving
offline acceptance unproved. The repair step completed and fresh runtime revalidation was
accepted by all completion gates. This is a product repair selected by the process, not
an engine exception or a special case for this application. The original QA's acceptance claim was therefore not
sufficient evidence that the generated app was correct.

Revalidation reports a clean restore/build, five passing generated-app tests, successful
worker activation with 70 cached assets, offline route/state restoration after the proof
host stopped, clean browser console, responsive screenshot review and process cleanup.
The runtime appended its accepted completion record to the revalidation artifact. This
proves local published-static behavior; external deployment and a future new-version
worker rollout are not claimed. The delivery step completed and the durable run summary
now reports **Succeeded / Complete**, six completed steps, three skipped branch steps,
zero failed steps, zero incidents and zero escalations. The original blocked attempt was
retried once through the native UI; this is not a claim of an uninterrupted first attempt.

## Validation

| Lane | Result |
| --- | --- |
| Debug AgentFramework module build | Pass, zero errors; existing warnings retained |
| Release Processes module build, including native agent dependencies | Pass, zero errors; existing warnings retained |
| Final Release Web build | Pass, zero errors, 189 existing dependency/nullable warnings; ordinary host refreshed from these outputs |
| Components: AgentChatSurface, AgentChatPanelResponsiveness, AgentAdjunctOwnership, ChatWorkspacePanel, ConversationFloatingComponents | 108 discovered and passed, zero skipped |
| Unit: BrowserRuntimeLifecycleCompletionGateContribution, ProcessLaunchPrompt | 50 discovered and passed, zero skipped |
| Shared OverlayWindow lifetime | 8 discovered and passed, including default disposal and retained editor state |
| Overlay runtime | 6 Node regression cases passed; included in `npm run assets:verify`, which passes |
| Validation tooling | 6 portability baseline, 4 secret scanner and 7 rendering-register tests passed |
| Parity browser, 1600 × 1000 viewport | 736 × 570 chat container; 48 px header and 111 px composer; draft preserved and focus restored after details close |
| Real Chromium resize runtime | Monotonic native resize samples, no callbacks during the first gesture; two successive gestures remain at the newest size with 500 ms delayed echoes and 40 ms content updates |
| Context dialog browser | Opening caused no intent; explicit detach changed the owned sample context; Escape returned focus |
| Narrow chat body | 480 × 300 body retains an 85 px scrollable transcript, no horizontal overflow, and the draft/focus after details close |
| Native layout | Same saved conversation and 760 × 720 window: transcript grows from 19 to 350 px; confirmed on Live Processes and Project Structure |
| Native details | Both dialogs restore opener focus and preserve draft; explicit historical read returns the old run's unsupported-receipt result visibly |
| Final native resize | Two twelve-sample gestures: 760 × 720 → 520 × 420 → 900 × 780; reset returns 760 × 720 |
| Final native editor/frame lifetime | Same editor and unsent draft survive minimize/restore, reset and both dialogs without workspace reload; body is hidden while minimized; frame offsets remain zero while transcript scrolls both axes |

The first component candidate passed 100/103. Two failures exposed hidden failed-run
badges and one exposed unreachable historical reads after detail-load failure. Both
production cases were repaired; the full 103-case selection then passed. The initial
source estimate omitted four expanded cases; discovery verified 103 before execution.
The final selection adds five conversation-window cases and passes all 108. Its first run
passed 107: the new test read AngleSharp's textarea property instead of Blazor's rendered
value attribute. Correcting that assertion required no further production change. The
initial shared-test invocation also required restoring stale NuGet assets before discovery;
all eight lifetime cases then passed.

Portability review found one intentional case-insensitive comparison of the existing
operation protocol name, not a filesystem path. Its one baseline addition was reviewed.
Final regenerated enforcement passes without `--write-baseline`: 15,713 reviewed
executable-source findings unchanged. Rendering-register enforcement passes for 1,212
inputs. Documentation validation passes for 439 maintained Markdown files. These are
focused lanes, not a claim that the complete repository suite ran.

The resize regression also verifies that a pending pre-reset geometry acknowledgement
cannot undo reset. A scratch comparison with the old runtime demonstrated the default-size
reset after an observed programmatic size change; its ordinary mouse gesture did not
reproduce the intermittent lag. The new runtime's delayed-echo browser case and Node
regressions establish the repaired ordering; they do not measure every browser's latency.

## Resource and evidence ownership

Ignored proof is in `.artifacts/concurrent-mutations/compact-chat-process-20261024`:
source receipts, build/discovery/TRX logs, component MCP queries, browser measurements,
screenshot and ownership records. It contains private local run evidence and is not a
published dataset. Components MCP query processes exited. Both uses of the owned Parity
sandbox on 5391 completed and were stopped; scratch browser tabs were closed. No database,
container or volume was created. The ordinary host was refreshed only after active work
completed, with a historical pending approval preserved. Its 5032 endpoint returns HTTP
200 with the reported run still Completed; the final 7271 UI passes the browser sequence.
The temporary browser draft was cleared without submission. The ordinary databases,
shared isolated PostgreSQL endpoint, installed MCP tools and SPA instance are preserved.
P29's separately recorded blocked Python fixture cleanup is unchanged.

## Width regression follow-up (2026-10-25)

The reported wide window exposed an intrinsic-width constraint in the conversation
wrapper's horizontal flex body. At a 979 px window width, the agent surface stopped at
765 px. Setting that existing body to `flex-direction: column` stretches its child across
the available width. This one-rule presentation fix adds no runtime handlers, server
calls or dependencies; the rendering-register hash and architecture record are updated.

Native Chromium proof uses the same saved conversation on Project Structure at the
1920 × 1080 desktop viewport. Twelve-sample pointer gestures exercise 520, 760, 1100 and
979 px window widths. Every sample progresses monotonically; the surface fills the body
and the composer fills the panel within one pixel of rounding. At the reported width,
the inner surface now measures 936 px; at maximum width it measures 1057 px and the full
table fits without horizontal transcript overflow. At minimum width, the wide table
remains horizontally scrollable. The outer frame stays at zero scroll offsets.

The same editor and unsent draft survive the gestures and minimize/restore. The final
lifetime assertion waits for Blazor's hidden-body attribute before checking identity;
the initial immediate assertion ran before that render. The fixture draft was cleared
without submission. Screenshots at the reported and maximum widths were inspected: the
unused right strip is gone, the header and composer are visible, and controls remain
readable. The conversation catalog also fills its available body width.

The Release Web build passes with zero errors and 194 warnings. The six baseline-tool,
four secret-scanner and seven rendering-register tests pass. Final portability-static
enforcement passes without baseline writes: 15,713 reviewed executable-source findings
unchanged. Rendering enforcement passes for all 1,212 inputs. This CSS repair uses native
browser proof; no additional .NET test suite is claimed.

Proof is retained under the existing ignored evidence root as `width-native-proof.json`,
`width-*-after.png`, `width-web-release.log` and `width-portability-final.*`. The ordinary
5032/7271 host was refreshed after verifying no active executions from its lifetime, and
returns HTTP 200. Ownership records identify its replacement launcher/runtime. No new
fixture, database or container was created; Components and SPA sources are unchanged.
