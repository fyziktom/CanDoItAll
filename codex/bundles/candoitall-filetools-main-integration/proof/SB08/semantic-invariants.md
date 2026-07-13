# SB08 Semantic Invariants

Date: 2026-07-12.

| Invariant | Required behavior | Positive evidence | Adversarial evidence |
| --- | --- | --- | --- |
| `SB08_INV_DISABLED_LITERAL` | Missing/disabled policy calls the native provider directly with zero cache interaction. | Disabled test performs two native calls. | Injected cache store throws on any lookup/bypass; it is never invoked. |
| `SB08_INV_RAW_PROVIDER_NOT_AUTHORITY` | Cached descriptive facts can be shared but never grant an effect. | Ordinary cached browse and authorized fresh occurrence pass. | Cached stale listing followed by native occurrence removal fails authorization before coordinator grant. |
| `SB08_INV_KEY_ISOLATION` | Runtime/profile, scope, source set/config, revision, container/query, cursor, sort, metadata, and budgets cannot collide. | Key-isolation test requires a new native call for each changed dimension. | Fixed emitted key contains only schema plus SHA-256; no raw external value is exposed. |
| `SB08_INV_BOUNDED_RETENTION` | Entry/item/continuation/byte/TTL/hard-lifetime bounds apply per storage and globally. | Capacity eviction and TTL tests pass; metrics stay within configured bounds. | Oversized UTF-8 payload and disallowed continuation bypass retention; 257-source session and oversized config are rejected before work. |
| `SB08_INV_COALESCING` | Same-key concurrent loads execute one native operation. | Two blocked concurrent callers plus a later hit produce one native call, one miss, two hits. | Cancellation and provider failure retain no page and retry reaches native provider. |
| `SB08_INV_REVISION_AFTER_PERSISTENCE` | Only successful persistence publishes a monotonic revision. | FileInteraction save bumps scope revision; placement bumps storage revision; aggregate selects new listing. | Conflict/failure/cancel and rejected placement preserve `(0,0)`. |
| `SB08_INV_RUNTIME_SWITCH` | A new runtime generation/profile/fingerprint cannot reuse the previous listing. | Mutable runtime-state test changes generation and requires a native call. | Old entry remains bounded but unreachable because its key differs. |
| `SB08_INV_IMMUTABILITY_HONESTY` | Config cannot make a mutable source immutable. | CID policy is structurally supported by provider/root checks. | MFS plus immutable policy throws typed `InvalidConfiguration`. |
| `SB08_INV_NO_DISTRIBUTED_FALLBACK` | No secondary cache runs without durable shared revision/backplane. | Memory entries set `DisableDistributedCache`. | Configured Hybrid mode throws typed `InvalidConfiguration`; there is no fallback to Memory. |

The invariants apply to listing facts only. File content, streams, handles, authorization decisions, and mutation payloads are never cache values.
