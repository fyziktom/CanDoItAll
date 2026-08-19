# Current state and target delta

| Concern | Current branch | Target after this bundle |
|---|---|---|
| Product ownership | Separate LLM Chats module | Preserve |
| Definition behavior | Immutable revisions | Preserve and prove |
| Conversation metadata | Product row plus transcript row | One writable canonical owner |
| Create/rename | Apparent UoW with independent EF store context | One command context and transaction |
| Turn admission | Transcript commit then evidence callback | One atomic admission command |
| Completion | Assistant commit then operation finalization | One atomic success command |
| Compensation | Best-effort retries, exhaustion not durable | RecoveryRequired on unresolved compensation |
| Idempotency | Existing op resolved after mutable lifecycle checks | Existing op/fingerprint resolved first |
| Cancellation | Durable request plus local CTS | Durable monotonic evidence checked at finalization |
| Profile identity | Lease around engine/provider work | Fence whole query/command lifecycle |
| Execution ownership | Process-local registry | Durable claim/lease/heartbeat |
| HTTP turn | Synchronous completed response | 202 admission plus durable operation |
| Provider output | Completed response only | True deltas where supported |
| SSE | Generic infrastructure exists, no Simple Chat events | Durable journal + profile-bounded replay endpoint |
| Query growth | Full transcript/full-document materialization | Keyset/paged read models and bounded context load |
| API provenance | Origin partially caller-controlled | Server-owned origin and explicit auth scopes |
| Proof | Focused green; stable gate 19 red; no head CI | CP0/CP1/CP2 plus final stable gate and CI |
