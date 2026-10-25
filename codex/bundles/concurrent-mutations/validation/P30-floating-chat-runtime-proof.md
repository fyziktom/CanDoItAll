# P30: floating chat and runtime QA evidence

Status: implementation validated in focused tests; native host refresh and reported-run
completion are still in progress.

Design: [floating chat and runtime proof](../../../../docs/architecture/concurrent-mutations/12-floating-chat-and-runtime-proof.md).
Engine branch: `concurrent-mutations`. Shared resize fix: Components branch
`codex/native-gantt-lifetime`, signed commit `0a944a3c`. SPA is unchanged and unmerged.

## Changes and boundaries

Focused agent chat places routine thread badges and saved-receipt review in Conversation
details. Context permissions, affinity and follow/detach actions use a separate controlled
dialog; blocked, detached and pending context remain visible. The compact composer uses a
single wrapping action row. The draft/editor and active conversation remain mounted.
Warning/error badges and approval actions stay in the primary surface. When conversation
loading fails, historical receipt review stays reachable beside the visible failure.
Full-page chat retains its previous presentation. Native owners still validate action
origins and perform reads/writes; disclosure itself performs neither.

OverlayLib keeps native resize local until release, ignores recent geometry echoes during
newer gestures, preserves explicit reset/minimize/visibility changes, and removes owned
listeners/timers on disposal. No pointer-move server calls or feature-specific resize
runtime were added. The existing bounded host and state contract remain in place.

The reported QA attempt had valid browser checks for its first preview host, followed by
a second start at the same URL for a service-worker update check. Its latest evaluation
receipt did not contain the host URL; earlier navigation preceded that start. The latest
host correlation gate therefore refused completion. The generic browser execution brief
now requires fresh navigation and proof after every start/restart, before the matching
startup receipt is stopped. Cached/offline proof remains supplementary. No application
name, route, business domain or acceptance criterion is embedded in the fix, and no gate
was weakened. Rework was requested through the native Live Processes dialog for only the
blocked QA step, with the same lifecycle instructions; completed upstream work is retained.

## Proof so far

| Lane | Result |
| --- | --- |
| Debug AgentFramework module build | Pass, zero errors; existing warnings retained |
| Release Processes module build, including native agent dependencies | Pass, zero errors; existing warnings retained |
| Components: AgentChatSurface, AgentChatPanelResponsiveness, AgentAdjunctOwnership, ChatWorkspacePanel | 103 discovered and passed, zero skipped |
| Unit: BrowserRuntimeLifecycleCompletionGateContribution, ProcessLaunchPrompt | 50 discovered and passed, zero skipped |
| Overlay runtime | 5 Node regression cases passed; included in `npm run assets:verify`, which passes |
| Validation tooling | 6 portability baseline, 4 secret scanner and 7 rendering-register tests passed |
| Parity browser, 1600 × 1000 viewport | 736 × 570 chat container; 48 px header and 111 px composer; draft preserved and focus restored after details close |
| Real Chromium resize runtime | Monotonic native resize samples, no callbacks during the first gesture; two successive gestures remain at the newest size with 500 ms delayed echoes and 40 ms content updates |
| Context dialog browser | Opening caused no intent; explicit detach changed the owned sample context; Escape returned focus |

The first component candidate passed 100/103. Two failures exposed hidden failed-run
badges and one exposed unreachable historical reads after detail-load failure. Both
production cases were repaired; the full 103-case selection then passed. The initial
source estimate omitted four expanded cases; discovery verified 103 before execution.

Portability review found one intentional case-insensitive comparison of the existing
operation protocol name, not a filesystem path. Its one baseline addition was reviewed.
Final regenerated enforcement passes without `--write-baseline`: 15,713 reviewed
executable-source findings unchanged. Rendering-register enforcement passes for 1,212
inputs. Native browser closure remains in progress.

## Resource and evidence ownership

Ignored proof is in `.artifacts/concurrent-mutations/compact-chat-process-20261024`:
source receipts, build/discovery/TRX logs, component MCP queries, browser measurements,
screenshot and ownership records. It contains private local run evidence and is not a
published dataset. Components MCP query processes exited. One owned Parity sandbox on
5391 supports UI proof; no database, container or volume was created. Preserve ordinary
5032/7271 and its databases, the shared isolated PostgreSQL test endpoint and installed MCP
tools. P29's separately recorded blocked Python fixture cleanup is unchanged.
