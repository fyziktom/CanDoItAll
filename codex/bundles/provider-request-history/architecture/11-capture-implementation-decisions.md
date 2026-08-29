# SB04 capture implementation decisions

This record amends the prepared boundary map before downstream execution. It does not relax
the capture, source ownership, security, or proof requirements. Implementation remains under
the user's 2026-08-28 authorization. SB05 owns canonical producer hooks and backfill.

## Typed observation placement

MAF's actual provider registration now supplies HistoryProviderDriverFactory. It decorates
eight closed typed driver contracts, uses their typed results/terminal events, and rejects an
unhandled contract. Relay dispatch passes through because ProviderManagement already owns its
canonical audit. Shared image dispatch carries a trusted SharedRelay owner and avoids a second
standalone row. No generic runtime handle inspects object payloads or TResult for usage.

The SDK path has a dedicated ProviderHistoryChatClient inside EmptyCompletionRetryChatClient.
The outer retry allocates one logical context and the inner observer allocates each durable
attempt. SDK internal HTTP retries remain opaque; the index reports application-visible attempts.
Legacy route, compatibility, tool and cancellation behavior remains in the existing adapters.

This placement replaces scattered duplicated observer implementations with one typed registry
seam in the existing runtime owner. New provider kinds reuse those contracts. New operation
contracts must provide a typed adapter; there is no silent unobserved default.

## Recorder ownership

HistoryInvocationRecorder lives in History.Persistence, alongside the durable capture store,
partition/profile fence, policy store and host lease. It performs EF-bound reservation wiring.
Pure terminal/identity/detail policy stays in History.Application. There is no reverse edge to
Models, MAF, drivers, Web or canonical stores. A second Application wrapper around that wiring
would add indirection without removing a dependency or improving its database-bound test seam.

## Chosen project edges

The prepared allowed-edge ceiling still applies, with these explicit clarifications:

- Core may reference History.Abstractions for the typed image request context only.
- Providers references History.Abstractions for typed request context, nullable wire evidence,
  remote request correlation and terminal-write failure retry classification. It does not
  reference History.Application or History.Persistence.
- Web directly references History.Abstractions for the validated caller request mapper.
- Models, Llm.Abstractions, Llm.ProviderRuntime, Maf and ProviderManagement consume the neutral
  contracts as prepared. SharedProviders.Abstractions remains BCL-only with zero project edges.
- No new package, runtime partial, Workspace reverse edge, or module-to-Providers edge is added.

## Actual batch retention

The current ProviderBatchJobCheckpoint retains status and result references, not an owned
conversation transcript; the only bundled checkpoint store is in memory. Checkpoint mode cannot
justify a canonical content owner. Batch attempts therefore default to no canonical owner and
use normal light/detailed policy, with one stable logical request per job/item across retries.
Recovery of completed items does not invoke or capture again. A real producer may supply a
canonical owner only when it owns retained content; SB05 must not invent BatchItem ownership
from a checkpoint. No production consumer of this generic balancer was found in the main repo.

## Caller and optional relation

Web maps validated managed-token registry identity, independently from subject, into a trusted
snapshot. Direct test-chat and its SSE endpoint overwrite ignored posted History fields. Shared
relay uses its own protocol caller shape, mapped in ProviderManagement. Raw credentials and
untrusted headers never become identifiers or authority. Auth-disabled/legacy/unknown states
remain explicit. Root agent/chat/workflow ownership and caller propagation belongs to SB05.

CanDoItAll-Request-Id is an optional bounded relation only when the runtime profile has a configured
shared SourceAccessToken binding. Remote source identity comes from that binding. SDK clients that
do not expose the response header have no relation; response body IDs are not substituted.

## Captured detail and terminal consistency

Detailed standalone input freezes a bounded in-memory credential snapshot at Begin, including
response-only requests. Rotation during inference cannot cause the old request credential to
escape response redaction. Weak attempt keys allow snapshots to be collected; no secret is persisted.
This is bounded known-secret removal, not universal DLP. Failure to protect text remains explicit.

Stream disposal records cancellation when needed, then permits monotonic reconciliation with
actual late terminal usage. Recovery/cancellation cannot erase stronger usage/price categories.
History terminal write failures must not trigger inference retries, including nested batch errors.

PostgreSQL timestamp precision is normalized at the storage boundary for identity and idempotence.
The deterministic sub-microsecond test reproduced a real SDK failure before the fix. Npgsql's
v10 PgTimestamp converter is the primary implementation reference:
https://raw.githubusercontent.com/npgsql/npgsql/v10.0.0/src/Npgsql/Internal/Converters/Temporal/PgTimestamp.cs

## Downstream obligations

Capture alone does not close history search. SB05 must attach actual canonical sources through
transactional outbox/file journal and keep aggregate lineage distinct from attempt accounting.
SB06 owns permission/query bounds. SB07 and SB08 still require both real UI scopes and standard5032
plus the existing Docker5210 publisher/5212 client. No live acceptance is claimed by this record.
