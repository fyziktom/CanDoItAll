# Forbidden patterns
- Write-side plugins call external systems directly from UI/workbench services
- No durable connector command/outbox boundary exists
- No idempotency/retry/replay semantics exist for connector commands
