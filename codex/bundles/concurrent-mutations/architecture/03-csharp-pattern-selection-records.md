# Pattern decisions

| Force | Selected approach | Rejected alternative | Independent test/proof |
| --- | --- | --- | --- |
| Cleanup can throw before releasing a lock | Existing resource owner with try/finally; preserve failure information | New lock framework or swallowing cleanup exceptions | Inject acquisition/disposal failure, reacquire same key, double-dispose |
| Whole replacement and scalar edit have different meanings | Typed owner commands and cohesive definition writer | One model with nullable magic flags, or scalar adapter inventing collections | Scalar retains children; replacement rejects foreign/unknown identities |
| Original state must survive transport | Typed expectation and explicit outcome | Server reads a fresh token for legacy callers | Missing/stale expectation rejected at every enabled route |
| Multiple module contexts commit one SQL effect | Existing coordinated transaction and typed owner participant ports | Global repository/DbContext, parallel enlisted commands, distributed transaction | Exact same transaction, rollback/no partial projection, retired participant denied |
| A response may be lost after commit | Atomic owner operation journal, semantic digest and exact result lookup | In-memory cache, precommitted generic InProgress manager, runtime journal as SQL truth | Concurrent duplicates, digest mismatch, restart/lost response |
| Model work depends on mutable facts | Trusted bounded basis plus owner publication validator | Global capture of all reads or model-selected mandatory dependencies | Material source/membership change rejects; unrelated data does not |
| Runtime reaction is configurable | Small validated enums/records and pure policy resolution | Prompt-only boolean, plugin per trivial mode, generic concurrency coordinator | Matrix, precedence, owner minimum, checkpoint budget persistence |
| UI acknowledgement races typing/navigation | Existing submission/live reconciler and host generation | Whole model replacement, universal form state service | Delayed ack/third writer/retired editor tests |

Each choice solves an observed force. No new service locator, nested provider family,
runtime partial, factory hierarchy or event bus is justified. Existing ProjectsService
may remain a facade only if the extracted mutation implementation is removed from it.
The second owner must prove common semantics before shared implementation is extracted.
