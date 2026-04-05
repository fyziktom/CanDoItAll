# Durable connector command boundary
Before implementing write-side connectors (email send, LinkedIn actions, custom remote API mutations), introduce a generic connector command boundary:
- connector command record / outbox,
- idempotency key,
- attempt counter / timestamps,
- retry/backoff policy,
- failure visibility,
- replay / dead-letter support,
- optional approval hooks.

Do not call external systems directly from UI handlers or workbench mutation services.
