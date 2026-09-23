# Provider request history

Provider History and the global Request history surface expose application-visible provider attempts and canonical execution evidence. Opening a panel is lazy: choose filters and search explicitly, then page through the bounded results. Use the detail action for authorized content. History is not a record of every physical SDK retry or every internal HTTP send.

## Identity and authority

A logical request can have several attempts. Retries retain the logical request ID and input revision; attempts have separate IDs, outcomes and response evidence. Preserve the typed invocation context when retrying. A managed credential is identified independently of its subject, so two tokens with the same subject remain distinguishable.

| Authority | Scope | Access |
| --- | --- | --- |
| Metadata | `api.provider-history.read` | Authorized searches and metadata |
| Content | `api.provider-history.content.read` | Separately authorized input/response details |
| Management | `api.provider-history.manage` | History policy changes |

These scope names are service/UI authorization contracts; there is currently no general remote provider-history HTTP API. Trusted local operator access is host-derived. Unknown callers do not gain content or management access. Canonical content additionally requires authorization for its owning source. Cursors and reads are bound to the current partition and authorization/runtime context.

External references, including a local project reference, are opaque correlation values. They are not authority, secrets, or a substitute for managed-token identity.

## Capture and privacy

Light is the default: metadata, usage, price and outcomes are retained without standalone prompt/response bodies. Detailed captures bounded current-turn input and current response when the owning flow permits it. It does not reconstruct prior conversation context.

Agent, Simple Chat, workflow and shared-relay content can have a canonical owner. History links to that owner instead of copying its bodies. Canonical availability and retention remain governed by the owner; missing/deleted content is reported explicitly.

Detailed text is redacted before encryption. Configured secrets and allowlisted credential forms such as password, api_key, client_secret, authorization and bearer tokens are masked, including quoted key/value syntax. This is not universal secret detection. Do not deliberately include credentials or unrelated personal data in prompts. Redaction uses a bounded credential snapshot; an unavailable protection context must not cause an unprotected write.

A redaction repair does not rewrite previously retained content. If sensitive data may have been captured, first disable new Detailed capture, rotate affected credentials, and use an explicitly reviewed retention reduction/cleanup operation. Inspect its preview and ownership implications; do not delete canonical conversations or protection keys as an automatic workaround.

## Retention and recovery

Default policy: 30 days metadata, 7 days standalone details, 32 KiB text per captured part, 256 MiB detail quota, and maintenance batches of 500. Policy changes are versioned. Applying shorter retention to existing data requires an explicit preview/decision; future-only changes do not silently erase existing records.

Quota accounting and detail attachment use the same partition lock. Expiry clears protected bytes and releases quota. Metadata cleanup removes eligible terminal rows in bounded batches; an input detail is deleted only when expired, empty and no remaining attempt references it. Retained retries keep their input reference. The logical input deadline survives tombstone deletion so a late retry cannot recreate expired input. Each attempt's response keeps its own deadline.

Canonical source projection, outbox delivery and backfill have separate progress/coverage evidence. Check failures, backlog, source checkpoints and host leases when rows appear incomplete. Maintenance runs every 20 seconds with a ten-second pass budget. At the default batch size, the outbox's idealized ceiling is 25 items/second; each source is separately capped at 100 items/pass (5/second), before database work and failures. Sustained arrivals above those limits grow a backlog. Size the deployment against measured arrival rate, backlog and checkpoint age; these ceilings are not throughput guarantees. Do not reset checkpoints or replay provider inference to conceal a history persistence failure.

A durable history start precedes provider use. If terminal history persistence fails after inference, report the failure without automatically sending the inference again. Interrupted host leases and recovery are distinct from observed success. Explicit provider deadlines are TimedOut; caller cancellation is Cancelled; already-observed terminal success/usage is preserved during late cancellation or disposal.

Usage and price fields distinguish unavailable, partial, unpriced and unsupported evidence from actual zero values. Prices use the tariff frozen for the attempt.

## Backup and validation

Back up the database together with the canonical content/files/journals required by its owning flows and the applicable data-protection/secret-vault material. Database-only restore may recover metadata while leaving canonical or encrypted content unavailable. Transfer validates partition, ownership, active-capture and quota constraints; it is not a body export or remote replay API.

Follow [backup and restore](operations/backup-and-restore.md) and [testing](testing.md). Focused owning tests are ProviderHistoryCaptureIntegrationTests, ProviderHistoryPersistenceIntegrationTests and ProviderHistorySourceProjectionIntegrationTests; browser verification uses the large-screen provider/global history, policy and detail surfaces.
