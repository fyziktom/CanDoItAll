# Stop conditions

Phase11 is **not** closed if any of the following remains true:

1. a scheduled plugin wakeup depends on request-path polling or manual calls.
2. operational messages are being represented as default Workbench nodes.
3. connector outbox processing still requires direct/manual invocation.
4. background work is still tracked but not actually drained by a runtime worker.
5. a single automation signal provider registration can shadow other module/plugin contributions.
6. Quartz is executing heavy plugin logic inline instead of publishing durable work.
7. inbound plugin envelopes are materialized directly without ingress dedupe/cursor handling.
8. MQTT is required for core internal orchestration to function.
