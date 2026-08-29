# Canonical producer mapping — implemented SB05

## Ownership and transaction boundaries

| Source | Actual producer | Durable handoff | Source reader / maintenance |
|---|---|---|---|
| Shared relay | SharedProviderInvocationAuditService, Finalizer and RecoveryService | Same-context audit write plus HistoryOutboxWriter; audit GUID and monotonic HistoryVersion | SharedProviderHistorySource |
| Simple Chat | EfLlmChatInvocationRecordRepository in the real operation unit of work | Invocation ordinal lineage plus operation content-owner mutation in the same transaction | LlmChatHistorySource / LlmChatHistoryDetail |
| Workflow | PersistentWorkflowUsageObservationStore and PersistentWorkflowResumeBoundaryStore | Exact attempt snapshots on the retained usage observation; same-context outbox | WorkflowHistorySource |
| Agent / process execution | FileSandboxWorkspaceJsonStore via the real execution-slice store | FileProviderHistoryJournal stages before every observed write/delete; exact source hash proves commit | AgentFileHistorySource / AgentHistoryPublicationStore |

History.Persistence owns scalar indexing and durable primitives. Source adapters stay with their
existing owners. Neither History.Application nor History.Persistence references concrete owners,
Web, Workspace, provider SDKs, or the old aggregate usage query.

HistoryMaintenanceContext holds the immutable runtime snapshot and partition. Its DatabaseAsync
runs each database phase under IDatabaseRuntimeWriteFence. File discovery, journal reads and
acknowledgments run outside that fence and outside an application-wide database lock. A profile
change rejects later database publication; old leases expire and original file intents remain.
The registered hosted worker rotates source order, gives each source two seconds and its pass
ten seconds, and records retryable checkpoint failure without preventing other sources' work.

## Identity and granularity

A logical invocation carries fresh request/attempt IDs and a bounded metadata-only handoff.
Agent observations keep the full invocation handoff once; other observations carry an explicit
included marker. Image preparation and constrained finalizer repair use that same context.
Direct workflow LLM execution supplies its own context and preserves success/failure/cancellation.
The existing runtime algorithms and usage aggregators remain their owners.

Exact attempt IDs are the only new-attempt joining key. Provider, model, timestamp, correlation
and text equality never deduplicate. Multiple owners link the same attempt rather than add its
cost again. Legacy aggregates remain aggregates with CanonicalRecorded time and unavailable
attempt/actual-start/caller information where absent. Aggregate prices never overwrite attempts.

Simple Chat invocation evidence uses operation GUID + ordinal. Content ownership uses the
operation GUID for both owner/evidence, cumulative exact attempts, and monotonically increasing
invocation ordinal as version. A pending owner suppresses detailed capture before transcript
commit. The real direct-SQL admission insert now persists the validated caller as well.

HistorySourceProjection preserves EntryId and SortAtUtc under updates. It considers tracked
owner additions/deletions inside a transaction, including removal of both last owners and
replacement by a new owner. The final review reproduced both bugs before this correction.

## File durability and locators

Source journals contain scalar facts, versions, hashes and deletion markers, never prompts or
responses. They live in .provider-history/<scope> outside removable run/project directories.
A prepared head retains previous committed evidence plus the highest reserved version. Recovery
checks the actual source-file hash/absence; it never assumes two file writes are atomic.
Acknowledgment follows the idempotent database commit. Failed publication leaves the intent.

AgentHistoryPublicationStore commits exact locator + scalar projection together. Locators retain
partition, original scope kind/key, owner/evidence and version. Deleted-project reconciliation
publishes newer tombstones; late file publication into a deleted project cannot revive entries.
The locator deliberately has no project FK so its deletion marker survives project removal.

Legacy binding is durable and one-time; a source cannot be reinterpreted under another partition.
Bounded manifest chunks and consumed offsets resume file backfill after restart. Default legacy
discovery is the current profile's organization plus its current projects. Unbound legacy tenant,
default or process roots are not silently assigned to this database. New exact evidence supplies
its own partition and the ready queue handles it.

## Caller meaning and content availability

The caller is the credential that initiated the logical run/operation. Approval continuations
and workflow resume retain that origin; this is not a claim about the credential of a later
approver. Agent/chat command JSON cannot inject trusted caller context. Workflow origin is
constructed by the API, and direct provider endpoints replace posted context. No person mapping,
bearer secret, provider secret, or token hash is stored.

Simple Chat has a bounded exact-turn content reader through its canonical messages. Agent and
workflow usage observations do not establish a bounded prompt/response reader: their history
content is explicitly Unavailable, not a fabricated transcript or duplicate prompt capture.
SB06 must check source existence and owner authorization before and after every content await.
The existing canonical histories remain the source of fuller run/conversation information.

## Retention and transfer

Relay Begin freezes the effective metadata retention from the partition policy when no explicit
internal retention override exists. Duplicate Begin does not recalculate its deadline. Relay
maintenance stages a newer delete and removes expired terminal audit rows in one transaction.
It never purges in-progress audits. The history worker now invokes bounded detail/metadata
cleanup after the deletion/replay gates. This is code activation, not deployment to a user host.
Canonical owners keep their lifetime; history-policy expired input never revives on a late link.

History transfer preserves storage lineage, source versions, owner links and protection metadata.
AgentHistoryTransferParticipant copies original file locators in bounded batches and rejects
missing retained target projects. It does not rebase scopes or copy source files/protection keys.
History-only transfer is a snapshot, not canonical graph migration or cross-profile deletion
federation. Source content can be unavailable until its original canonical store is accessible.

Simple Chat transfer schema8 preserves caller and exact attempts, stages source intent in its
target transaction, and rejects foreign-lineage attempts. Replacing retained indexed chat data
is rejected instead of reusing source versions; a fresh target is required. The separate history
group must establish matching lineage first when transferring already indexed evidence.

## Coverage and downstream gates

Registered canonical kinds are AgentConversation, SimpleChat, Workflow and SharedRelay. Process
workloads use agent/workflow owners; an in-memory batch checkpoint is not canonical ownership.
SB06 coverage must use registered sources, not unused Process/BatchItem checkpoint enum slots.
Index coverage does not prove detail availability or complete legacy attribution.

The source/journal/storage checks are recorded in proof/SB05. Search SQL, principal/resource
authorization, policy UI and requested 5032/Docker acceptance remain SB06–SB08. No UI or live
deployment result is inferred from these source tests.
