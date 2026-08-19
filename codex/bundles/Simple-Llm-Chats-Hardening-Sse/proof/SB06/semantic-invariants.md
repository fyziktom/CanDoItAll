# SB06 / CP1 semantic invariants

1. Conversation and transcript metadata have one canonical writable owner.
2. Create, rename, admission, finalization, exact compensation, usage/evidence, and operation state use
   the scoped fenced unit of work when they form one product transition.
3. Operation identity/fingerprint is resolved before mutable lifecycle validation. Cancellation is
   monotonic, and unresolved compensation becomes `RecoveryRequired`.
4. Every public use case retains one runtime profile identity from first read through final return or
   commit; stale-generation writes fail closed while committed evidence remains durable.
5. Only the dispatcher-owned `LlmChatOperationExecutor` calls `InvokeTurnAsync`. There is no public
   engine method that performs admission, provider work, and completion inline.
6. Durable lease owner/epoch/expiry and database heartbeat/cancellation evidence are authoritative;
   local signals and cancellation tokens are latency optimizations.
7. Definition/conversation/transcript collections and provider context remain bounded canonical reads
   with deterministic keysets and no per-item lookup loop.
8. Database transfer and migrations describe the same canonical schema at the checkpoint head.
