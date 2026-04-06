# Behavior guardrail gates

The implementation must prove at least these behaviors:

1. a trigger fire survives process restart and still produces durable work.
2. a failed internal message delivery retries and then dead-letters idempotently.
3. connector outbox pending commands are drained automatically by a worker.
4. background work is actually drained by a worker instead of only being tracked.
5. multiple automation signal sources contribute concurrently without shadowing each other.
6. ingress envelopes can remain unmaterialized until an explicit materializer runs.
7. core execution still works when the MQTT bridge is disabled.
