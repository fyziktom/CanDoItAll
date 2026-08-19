# Canonical model

## Aggregate 1: LLM chat definition

### Current row

`LlmChatDefinition`

- `Id`
- `Name`
- `Summary`
- `AvatarImageUrl`
- `Status`
- `CurrentRevision`
- optional canonical organization scope selected at SB00
- `CreatedAtUtc`
- `UpdatedAtUtc`
- `ConcurrencyToken`

`LlmChatDefinitionStatus`

- `Draft`: editable; cannot start a normal conversation;
- `Active`: new and existing conversations may run;
- `Suspended`: kill switch; no provider dispatch;
- `Archived`: read-only catalog/history; no provider dispatch.

### Append-only revision

`LlmChatDefinitionRevision`

- `DefinitionId`
- `Revision`
- presentation snapshot: name, summary, avatar;
- behavior snapshot: system prompt;
- provider snapshot identity: profile ID, kind, display name;
- model;
- validated model settings:
  - temperature;
  - optional typed thinking-effort override (`null` = provider default; explicit `None` = disable when supported);
  - canonical provider-neutral model-parameter envelope;
  - timeout;
  - optional response format;
- deterministic settings fingerprint;
- created timestamp and reason.

Every behavior or presentation save appends a revision. Existing conversations never follow
`CurrentRevision` implicitly.

## Aggregate 2: product conversation metadata

`LlmChatConversation`

- `Id` — same GUID as the generic transcript document;
- `DefinitionId`;
- `DefinitionRevision`;
- `Title`;
- `Status` (`Active`, `Archived`);
- `OriginKind` (`Application`, `Api` in this bundle);
- optional canonical organization/subject ownership selected at SB00;
- `CreatedAtUtc`;
- `UpdatedAtUtc`;
- `ConcurrencyToken`.

This aggregate does not own transcript entries. It references the generic transcript stored by
`ILlmConversationStore`. A later deployment bundle may add an `ExternalChannel` origin and a separate
deployment/source association without changing canonical transcript identity or message rows.

## Aggregate 3: operation

`LlmChatOperation`

- strongly typed operation ID;
- conversation ID;
- kind (`SendTurn`, `Cancel`, `Recover`);
- request fingerprint;
- expected transcript revision;
- lifecycle status;
- cancellation-requested timestamp;
- execution evidence timestamps: turn admitted, provider dispatch started, provider dispatch returned,
  and transcript completed;
- started/completed timestamps;
- resulting transcript revision and assistant entry ID;
- stable sanitized failure code;
- concurrency token.

The operation ID is also the generic transcript turn ID. That identity permits deterministic
reconciliation after a crash.

## Immutable invocation record

`LlmChatInvocationRecord`

- operation ID;
- provider/profile ID and kind snapshot;
- model;
- requested and effective thinking effort when known;
- logical invocation ordinal;
- known aggregate input/output/cached usage;
- outcome and stable failure kind;
- started/completed timestamps;
- correlation ID.

It is append-only. It records known usage even when the assistant message is compensated away.

## Value objects and helpers

Use focused value objects:

- `LlmChatDefinitionId`
- `LlmChatDefinitionRevisionNumber`
- `LlmChatConversationId`
- `LlmChatOperationId`
- `LlmChatRequestFingerprint`
- `LlmChatSettingsFingerprint`
- `LlmChatRuntimeIdentity`

Use deterministic helpers:

- name/tag normalization;
- first-message title fallback;
- canonical request fingerprinting;
- settings fingerprinting;
- stable API cursor encoding/decoding;
- sanitized error-code mapping.

Thinking-effort capability is not canonical product truth. It remains a live provider/model
capability resolved from the existing provider policy. The definition revision snapshots only the
user's typed override and includes it in the deterministic settings fingerprint. An explicit override
must be valid for the selected provider/model both when the revision is saved and immediately before
dispatch. API option projections may expose capability status, control mode, allowed efforts, and
provider default, but they must not persist a duplicate catalog.

Do not create parallel message roles, provider kinds, invocation usage, or transcript entries. Reuse
the existing lightweight LLM canonical types.
