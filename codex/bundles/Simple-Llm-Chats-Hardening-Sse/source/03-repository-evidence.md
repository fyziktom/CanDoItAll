# Repository evidence

## Reviewed refs

| Item | Value |
|---|---|
| Repository | `fyziktom/CanDoItAll` |
| Feature branch | `simple-chats` |
| Feature head | `16b6aa4b60dc88a6134dd6c9c9e634c064ac5847` |
| Current development head at review | `eb6be3ea38075b442d24976655f5c45ac08bd6b5` |
| Merge base | `c0117109c6ef6166d1d8b1b42d75e7f4af83c5ee` |
| Feature branch relation | one commit ahead and one commit behind development |
| Feature commit message | `phase1` |
| Development-only delta | documentation commit named `info about legacy db update` |
| Commit status at feature head | no status contexts returned |
| Workflow runs at feature head | none returned |

SB00 must re-query all values; this file is review evidence, not a frozen execution assumption.

## Main production surfaces inspected

- `src/Modules/CanDoItAll.Modules.LlmChats`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers`
- `src/App/CanDoItAll.Web/Api/LlmChatsApi.cs`
- `src/App/CanDoItAll.Web/Api/LlmChatOperationsApi.cs`
- existing API streaming infrastructure under `src/App/CanDoItAll.Web/Api/Streaming`

## Existing first-wave proof inspected

- `codex/bundles/Simple-Llm-Chats-Backend-Api/EXECUTION-PROGRESS.md`
- `codex/bundles/Simple-Llm-Chats-Backend-Api/reviews/FINAL-MERGE-DECISION.md`
- `codex/bundles/Simple-Llm-Chats-Backend-Api/subbundles/SB11-final-regression-and-release-gate/SESSION-HANDOFF.md`

Those records say focused Simple Chat tests and the Release build passed, but the stable filtered solution
test ended with 8,121 passing and 19 failing tests. The first bundle therefore closed as Not Ready.

## Existing SSE infrastructure inspected

- `ProfileBoundedReplayEventStream<T>` creates a bounded replay stream per active database profile and
  generation and cancels old listeners on profile change.
- `ServerSentEventResponseWriter` handles `Last-Event-ID`/cursor parsing, replay gaps, heartbeat comments,
  response flushing, no-cache/no-store, `X-Accel-Buffering: no`, and disconnect cancellation.
- `WorkflowRunEventsApi` demonstrates the current endpoint pattern.

The new Simple Chat SSE endpoint must extend this infrastructure rather than introducing a second
generic SSE implementation.
