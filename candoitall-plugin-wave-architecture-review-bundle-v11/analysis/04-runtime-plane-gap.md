# Runtime plane gap

## What is missing
Before a larger plugin wave starts, the platform needs a durable runtime plane that can do all of the following without request-path polling:

1. wake work up on time,
2. publish internal events and commands,
3. route them to subscribed handlers,
4. retry failures safely,
5. dead-letter poisoned work,
6. drain connector outbox commands automatically,
7. ingest external polling/webhook/email envelopes before they become nodes,
8. expose observability for operators and future dashboarding.

## Why the current repo is not enough yet
The repo already has domain-side and persistence-side pieces, but the execution side is still missing:

- no hosted service workers,
- no canonical scheduler boundary,
- no Quartz integration,
- no durable pub-sub abstraction,
- no plugin ingress inbox,
- no runtime-driven connector outbox drain loop,
- no real background worker that consumes queued work.

## Practical consequence for the next plugin wave
Without phase11, every new plugin that needs time-based wakeups, event fan-out, or external polling will tend to invent its own mini-runtime.
That would fragment the platform before the plugin ecosystem even starts.

## Phase11 objective
Introduce one shared execution substrate so plugins only implement domain logic and materialization handlers, not their own schedulers, brokers, retry loops, and dedupe stores.
