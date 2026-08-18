# SB10 Semantic Invariants

## Activation and composition

- `/chats` is declared only by `LlmChatsPage`; the route is advertised only by `LlmChatsShellNavigationContributor`.
- The page composes the existing Conversations and Definitions owners and does not duplicate application behavior.
- Floating Simple Chat integration remains absent until CP2 passes.

## Definition and revision behavior

- Replacing definition tags twice in one unit of work cannot leave conflicting tracked row identities.
- A stale edit produces a sanitized optimistic-concurrency failure and cannot overwrite the newer revision.
- Reload replaces the form model and the browser control subtree, so dirty DOM values cannot survive authoritative refresh.
- Existing conversations stay pinned to their selected definition revision after later definition edits.

## Streaming and cancellation

- Each durable event session owns a service scope distinct from the Blazor circuit scope.
- Session disposal closes the inner event session and its scope; it does not cancel durable work.
- Explicit cancellation waits for any in-flight provider move to settle before disposing the async enumerator.
- Refresh follows the persisted active-operation identity and never redispatches the turn.

## Presentation and safety

- The 1600x1000 page has an explicit bounded workspace; transcript and dialog bodies own only their local overflow.
- Provider and persistence exceptions cross the UI boundary only as sanitized failures.
- Browser console and clean server-log windows contain no new warnings or errors after cancellation/reconnect proof.
