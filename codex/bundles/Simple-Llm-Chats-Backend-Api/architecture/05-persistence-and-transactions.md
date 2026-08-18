# Persistence and transaction design

## Table set

Use the existing module prefix convention.

| Table | Purpose |
|---|---|
| `LlmChats_Definitions` | mutable definition catalog row |
| `LlmChats_DefinitionRevisions` | append-only immutable behavior snapshots |
| `LlmChats_DefinitionTags` | normalized searchable tags |
| `LlmChats_Conversations` | product conversation metadata |
| `LlmChats_Transcripts` | generic conversation document root and active-turn state |
| `LlmChats_Messages` | ordered canonical transcript entries |
| `LlmChats_Operations` | durable idempotent command lifecycle |
| `LlmChats_InvocationRecords` | immutable known usage/outcome audit |

Do not create unused Project Structure context, attachment, deployment, moderation, or channel tables in
this bundle.

## Cross-process transcript CAS

`EfLlmConversationStore.ReplaceAsync` must be safe across application instances.

Required transaction:

1. begin database transaction;
2. issue a conditional transcript-root update where
   `TranscriptRevision == expectedTranscriptRevision`;
3. require exactly one affected row;
4. apply deterministic message delta:
   - append admitted user entry;
   - append assistant entry;
   - or remove only the exact pending user entry during compensation;
5. update active-turn and acceleration envelope;
6. commit.

A scoped in-memory lock is not sufficient.

## Message ordering

Persist a monotonically increasing sequence number per transcript. Enforce unique indexes for:

- transcript + sequence;
- entry ID;
- transcript + turn ID + role where compatible with current transcript semantics.

Do not derive ordering from timestamps.

## Definition revisions

- composite key: definition ID + revision;
- immutable after insertion;
- definition row points to current revision;
- conversation row has a restrictive foreign key to its exact revision;
- definition “delete” is lifecycle archive, not cascade deletion.

## Operation idempotency

Unique operation ID is mandatory. Persist a canonical request fingerprint containing:

- conversation ID;
- expected revision;
- normalized user text hash;
- effective definition revision;
- model/settings fingerprint;
- relevant option identifiers.

Same operation ID + same fingerprint returns/reconciles the original operation.
Same operation ID + different fingerprint fails with a stable conflict.

Raw prompts and credentials must not be placed in log messages or fingerprint diagnostic text.

## Migration

- append a normal EF migration after `20260728161028_InitialPostgreSqlBaseline`;
- do not edit the baseline;
- update the model snapshot;
- use provider-neutral EF mappings unless a PostgreSQL-only index is genuinely required;
- validate pending model changes;
- test upgrade from baseline, empty database bootstrap, and restart.

## Database transfer

Register an LLM Chats database-transfer handler through the existing canonical transfer mechanism.

The versioned payload must preserve:

1. definitions;
2. definition revisions and tags;
3. product conversations;
4. transcript roots and messages;
5. operations;
6. invocation records.

Import order follows those dependencies. It preserves IDs/revisions and validates duplicates and
referential integrity. Provider profile rows remain owned by their current module; the LLM Chat
payload stores stable provider references/snapshots and execution revalidates them after import.
