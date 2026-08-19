# Negative and source guards

## Current source assertions

| Assertion | Result |
|---|---|
| `rg -n "StreamTurnAsync"` over executor and concrete engine | Production executor consumes the streaming seam at executor line 120; engine owns the stream method at line 137. |
| `rg -n "InvokeTurnAsync\|ILlmInvocationPort"` over executor and concrete engine | Exit 1, no matches. The completed-only direct execution path is removed. |
| `rg -n "Microsoft\.AspNetCore\|CanDoItAll\.Web"` over both affected project files | Exit 1, no Web/SSE reverse dependency. |
| `rg -n "partial (class\|record\|struct)"` over both affected production projects | Exit 1, no production partial expansion. |
| scoped TODO/FIXME/NotImplemented/test-only/fixture/stub scan over journal production owners | Exit 1, no matches. |
| event-row field inspection | Only operation id, sequence, typed kind/state/attempt/delivery/outcome, bounded text/model/stable failure code, usage, and timestamp are persisted. |

## Adversarial cases

- unknown raw failure text containing `secret=credential` is reduced to
  `llm-chat.storage-corrupted`; the raw value is absent from the event;
- a text append without the current matching durable lease is rejected; production executor supplies
  owner/epoch and the repository rechecks status/lease expiry while the operation is locked;
- provider pause beyond the 25 ms coalescing window persists the small chunk before terminal output;
- response character/byte or durable event-count overflow throws stable
  `llm-chat.stream-limit-exceeded` and the audited enumerator records the abandoned active attempt;
- a provider failure after partial text keeps the delta and failed terminal evidence but no canonical
  assistant message;
- transaction rollback discards both the row and its local notification;
- retention cannot select Running, CancellationRequested, or RecoveryRequired operations because it
  filters only Succeeded, Failed, and Cancelled parents older than `CompletedAtUtc`.
