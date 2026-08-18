# User Agent Chat regression handoff

## Setup

1. From `C:\repositories\CanDoItAll`, start `src/App/CanDoItAll.Web/CanDoItAll.Web.csproj` with the normal local database and configured Agent providers.
2. Use a large desktop viewport, preferably 1600 × 1000 or larger.
3. Open `/agents?tab=chat` for the primary Agent Chat surface.
4. Use the left-shell `Agent chats` action for floating chats.
5. Open `/agents?tab=agents` for identity/runtime settings and `/agents?tab=floating-chat` for floating lifecycle settings.
6. Open `/processes`, select an existing definition, then select `Manager chat` for the Process consumer.

## Manual regression

- Catalog: search/filter/favorite, switch Agent, open a new thread, and open history.
- Sessions: select/search/create/rename a thread and confirm transcript, copy, timestamps, token metadata, focus, and scrolling.
- Execution: send a normal and long prompt; inspect busy, completion, runtime detail, stop/cancel, failure/retry, approval approve/reject, and auto-approval behavior.
- Composer: stage/remove/send an attachment; insert a Prompt Gallery item; verify configured voice or the explicit unavailable state.
- Floating: search/select/start, history, active switching, detach/follow, hide/keep/reopen, close/stop, retention/capacity, placement, focus, layering, and internal scrolling.
- Settings: load/save identity, avatar, summary, instructions, provider/model/default/override, thinking, status/history/approval, Agent-only sections, and floating lifecycle/preparation settings.
- Consumers: verify contextual Agent workspace windows and Process Manager chat.

## Codex-confirmed final run

- Main actual response: `MAIN AGENT CHAT OK`.
- Floating actual response: `FINAL FLOATING AGENT CHAT OK`.
- Floating hide/reopen/history/detach/follow/stop-to-zero: passed.
- Identity/provider/model/save: passed with unchanged values.
- Process Manager chat consumer: loaded and interactive.
- Console: 0 errors, 0 warnings.

## Known constraints

- Voice depends on a configured browser/audio provider and remains a user-environment check.
- Approval prompts, failure recovery, concurrency limits, and destructive Agent settings require suitable live scenarios and should not be manufactured against production data.
- Three unrelated Stable failures remain in untouched LlmChats integration tests; see `proof/SB09/final-test-execution.json`.

Record approval or defects in `reviews/user-regression-handoff.md`. Do not activate the Simple Chat UI phase without explicit user approval.
