# 6. Persistence, transactions, and migrations without duplicate truth

## Initial target

Keep one physical PostgreSQL database per data profile, bounded runtime models according to real domains/transaction boundaries, and one coordinated migration history with a complete design-time schema model. Preserve table names and IDs unless a specific approved data migration requires otherwise.

Fifteen contexts that still scan every model assembly are not separation. Each runtime context needs explicit owned mappings. Verify actual EF entity types, including navigation-discovered types, rather than merely inspecting DbSet declarations.

| Area | Runtime writer/model |
|---|---|
| Agents definitions/configuration | Agents persistence; existing execution-store seams may remain separate. |
| Providers/publications/history | Providers persistence; historical namespace prefixes do not justify duplicate masters. |
| CRM | Parties, HR, governance, bindings, participation; task assignments move to Work Management by meaning. |
| Projects | Projects/phases/hierarchy/lifecycle; CRM references through contracts. |
| Structure and Work Management | One context or genuinely separated internal contexts; do not split one aggregate into competing writers. |
| Processes/Workflows | Preserve their existing runtime/definition/checkpoint persistence and narrow access. |
| Prompts, Resources, TestLab, Scheduler, Collaboration, Plugins | Explicit mappings for each actual owner; no context/project quota. |
| Simple Chats and Memory | Retain already separated application/persistence boundaries rather than pull them into AppDbContext. |
| Workspace/control plane/Storage/Security | Follow actual lifecycle and backend; not everything must be EF. |
| Schema composition | Complete design-time model only, never a business-service escape hatch. |

## Preserve behavior, not just columns

The inspected AppDbContext scans registered assemblies, stamps modified IHasConcurrencyToken GUIDs, and uses a registry-dependent model cache key [SRC-023]. Preserve relevant stamping/model identity in new contexts. Opaque GUID concurrency tokens are not monotonic event sequence numbers.

Inventory actual interceptors, query filters, audit, soft-delete, time conventions, converters, owned types, indices, unique constraints, foreign keys/cascades, defaults, and tracking behavior. This document does not claim every mechanism exists everywhere; the affected slice must discover them. Pooling/cache keys must not retain another scope's fields or secrets.

## One migration authority

Modules own canonical mapping contributions; complete design-time composition includes them explicitly. A table must not be independently governed by both EF migrations and runtime SQL initialization. Compare actual schema/history and all supported upgrades before removing an initializer.

EF migrations compare the model against a previous snapshot [EXT-006]; a subset sandbox is not a production migration source. A development host may use canonically prepared schema with subset runtime services. EnsureCreated or ad hoc CREATE TABLE is not a replacement upgrade history. ExcludeFromMigrations leaves an entity usable in the runtime model and is not read-only protection [EXT-002]. An explicit reporting model, where justified, must not create a foreign-write backdoor.

## Cross-domain foreign keys

Do not remove physical foreign keys wholesale. A database integration constraint need not require a foreign CLR navigation. Complete schema composition may retain the constraint while a consumer carries an opaque reference. Verify the actual migration-stack mechanism and do not duplicate table mapping as an improvised workaround.

For each changed relationship define scope, existence, cardinality, lifecycle owner, deletion/archive behavior, historical reference, and import remapping. Removing a foreign key requires an explicit race-validation, tombstone, and reconciliation plan. Separate contexts alone are not sufficient justification.

## Short cross-context transactions

An owner defines its operation boundary. A named application coordinator may preserve an invariant spanning owners, such as mandatory assignment plus reservation. Infrastructure enlists contexts in the same DbConnection **and** DbTransaction; equal connection strings or DI scope are insufficient [EXT-001]. Do not pass DbTransaction through public domain contracts.

Call owners in a defined order and confirm the overall commit only after all required writes. SaveChanges in one enlisted context is not the final commit. Never perform irreversible provider I/O or publish events before that commit. Rollback must not leave tracked objects presented as committed state.

Retry the correct coordinated unit using stable operation IDs. After uncertain commit, query receipts before repeating. Validate lock order/isolation and conflict behavior on real PostgreSQL, not EF InMemory. Repeated enormous cross-domain transactions are a reason to reconsider the boundary, not create an omniscient UnitOfWork.

## Outbox, inbox, and command receipts

Source owner saves state plus publish intent atomically. Delivery may repeat; consumer saves its dedupe marker and projection/effect in one transaction [EXT-004, EXT-005]. Acknowledging before a durable effect is not processing. Leases schedule workers; owner validation and unique constraints enforce correctness.

New durable event metadata includes identity, source owner/entity, schema version, scope/incarnation, source revision/sequence, audit time, correlation/causation, and sanitized payload. Timestamps are not ordering authority. Checkpoints identify stream/partition/incarnation. A per-aggregate sequence is valid only with defined dependency handling.

An external broker and event sourcing are not required. A DB-backed dispatcher and transient invalidation can coexist. Outbox alone cannot make non-idempotent remote effects exactly-once.

**Command receipt placement matters:** saving an integration receipt after a non-idempotent owner create leaves a crash window. For new retry-safe automation, the owner must atomically commit the domain effect and its receipt/unique business intent, or participate in a genuinely coordinated transaction. Otherwise explicitly report uncertain outcome and reconcile. The existing Simple Chats definition request shapes do not supply this guarantee [SRC-034]; see [17](17-operation-protocol-and-multi-owner-journeys.md).

## Lifetime

Use short per-operation contexts/factories. UI sessions, caches, and snapshots hold no tracked entities or contexts. Server-side Blazor scoped lifetimes can span a circuit and DbContext is not intended for concurrent use [EXT-003]. Long runs use short admission/checkpoint/completion transactions, not a transaction across provider I/O or human approval.

## Cutover protocol

**Inventory and protect:** identify every UI/tool/HTTP/job/import/seed writer, constraints, representative data, current SHA/schema, and durable payloads. File-count tests are not invariant proof.

**Expand:** add compatible contracts/mappings only as needed. Route legacy APIs to the same owner. Read-only shadow comparison is allowed; shadow mutation or dual master writes are not.

**Reconcile:** report conflicting identities/bindings/roles and apply an explicit resolution policy preserving audit, provenance, and defaults. Never silently overwrite ambiguity.

**Switch:** atomically change admission for the operation/scope; block/delegate old writers and drain/fence workers. No interval with independent active masters.

**Verify:** compare identities, reference counts, meaningful hashes, constraints, old exports, repeated import/recovery, and product journeys. Exercise old and new durable schema versions, not only a build.

**Contract:** remove old implementation after production routing is proven. Compatibility serializers/redirects may remain only for a documented purpose and removal condition.

**Rollback:** specify compatible binary/schema pairs and the last safe reversal point. New persisted shapes may require forward repair rather than destructive Down. Backup restore must reconcile old effects before restarting workers. Rollback never authorizes two writers.
