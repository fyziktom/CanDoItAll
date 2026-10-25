# Accepted Process source projection

## Decision and measured reason

The [baseline](../validation/2026-10-20-accepted-source-scaling.md) allocates 31 MB for
one 1024-step original-source observation. Replace that repeated whole-launch decode
for newly accepted launches with independently hashed, immutable original-source evidence.
Its read cost must depend on the selected source metadata, not plan width or prompt size.
Keep current claims, generation, material stamps, source permissions and executor
permissions live. This change does not authorize dispatch by itself.

## Current owners and responsibility inventory

Scoped CodeAnalytics snapshot `snap-20261020114836-6de05ed1` remains current for all 5327
production inputs. Its three projects have no cycles; 833 types, 7907 members and 190
diagnostics remain. The Persistence inventory and exact symbols confirm the existing
query, prepared-launch store, codec and unit of work as owners. A focused usage query
returned no context; direct source inspection supplies that missing evidence.

`ProcessPreparedLaunchCodec` owns the complete reviewed launch format and its integrity.
`EfProcessPreparedLaunchStore` prepares it and updates continuation metadata; normal
writers do not replace its immutable payload. `EfProcessRuntimeUnitOfWork` accepts the
launch and creates runtime state atomically. `EfProcessExecutionAuthorityQuery` currently
owns SQL observation plus whole original-launch interpretation. Its selected binding
helper validates exact child/material authority. `EfProcessWorkflowDispatchAuthority`
also decodes the accepted launch and is a later consumer of the same storage seam.

Existing evidence covers selected dispatch, original receipts, publication, original
policy, timestamp compatibility and current revocation. Missing proof is narrow immutable
source capture, versioned persistence, corruption refusal, atomic rollback, old-row
compatibility and width/prompt-independent source reads.

## Boundary and pattern selection

Keep the new records, pure codec and EF reader/capture inside Processes.Persistence.
They represent storage evidence, not a new domain policy or UI read model. Add no project,
interface, registration, runtime partial, service locator or new dependency. Existing
Application/Runtime contracts and module composition remain unchanged. The migration
owner consumes the existing persistence model as before.

Use a pure codec for projection construction/validation and a cohesive persistence
adapter for the atomic write and exact read. Remove full-launch interpretation from the
Agent query when wiring the adapter. The unit of work gains a capture call at its existing
acceptance point; it must not acquire projection serialization or query-building helpers.
Pure codec tests must construct neither the query nor the unit of work.

Rejected alternatives: a global authorization cache has freshness, retention and memory
risks; request caching still scales with the whole plan; source generation or incremental
hashing does not remove graph decoding; SQL JSON extraction/full-payload hashing shifts
the same unbounded work into the shared database; storing original fields in mutable
runtime assignments couples historical evidence to assignment repair/deletion.

## Storage and authority semantics

Version 0 explicitly means the existing accepted full-launch format. Version 1 means
independent accepted-source evidence. Add a typed version plus nullable header JSON and
header digest to the prepared-launch row. Add assignment-source rows keyed by
`(AdmissionId, StepInstanceId)` with an FK to the prepared launch and cascade cleanup.
The header is stored once, avoiding repetition of a possibly large source authority.

The header binds admission, run, root run, plan, plan hash, definition version, project
lifetime, original source authority and the unchanged preparation fingerprint. Each
assignment record binds run, plan, step, key, executor, readiness, original timestamp,
allowed operations, operation scope, launch variables, capability scope, decision-policy
admission and optional Workflow binding. Prompts, unrelated steps, review/rendering data,
runtime state collections and complete material payloads are excluded. Keep typed fields;
do not manufacture a full assignment with empty placeholder fields.

Separate versioned hash domains protect header and assignment bytes; the assignment
digest includes the header digest. Decode only the declared version, reject unknown
fields/versions, validate identities and selected policy binding, and enforce a 1 MiB
UTF-8 ceiling separately for header and selected assignment metadata. This is an explicit
new admission bound; oversized metadata fails before acceptance without silently choosing
version 0. Prompt size is unaffected. Existing accepted version-0 data retains its format.

Version-1 source evidence is authoritative for source observation after atomic acceptance.
The full launch remains authoritative for launch review/continuation and retains its own
integrity validation. Corrupting that other copy cannot replace the accepted source.
Corrupt/missing source evidence fails explicitly. This is not a cache with fallback.
Application writes never update the captured source. Hashes detect inconsistent stored
evidence; they do not claim protection from an administrator rewriting all database data.

## Atomic capture, reads and retention

Capture from the exact validated original prepared launch at its first acceptance, under
the existing locked admission and coordinated runtime transaction. Header, all assignment
records, version marker, runtime state, accepted timestamp and existing receipts commit
together. A failed admission leaves no source records. Duplicate acceptance preserves
the original records; it must not recapture a repaired assignment or issue a second grant.
Existing accepted rows stay version 0; prepared old rows accepted after upgrade capture
version 1. Creation remains O(plan size), paid once at the existing acceptance boundary.

Read the exact header and assignment with one indexed join and select only required
metadata. A SQL conditional may include full payload only for explicitly version-0 rows.
Never catch version-1 validation failure and retry the legacy path. Preserve the existing
four-query selected observation shape, fingerprint field order/bytes and timestamp
normalization. Read current claims and permissions as before. Historical observation
continues to ignore repaired current assignment content and always denies dispatch.

Cascade cleanup follows the prepared-launch owner; there is no independent expiry worker
or global cache. The migration adds nullable fields/default version 0 and an empty table,
without scanning/re-encoding every retained launch under a migration lock. Downgrade must
refuse while version-1 evidence exists, rather than discard authoritative records. A later
explicit bounded maintenance operation may upgrade retained version-0 launches from
verified original payloads; no automatic read-path backfill or startup-wide scan is allowed.
Legacy reads retain their measured cost until that operation is implemented and qualified.

## Implementation checkpoints and proof

1. Build the pure records/codec with isolated positive, mismatch, unsupported-version,
   corruption and byte-limit tests. Repeat the five shape measurements against encoded
   header/selected-assignment reads in three fresh processes. This foundation alone does
   not change the running source path or unlock the producer.
2. Add model/migration and atomic capture; wire the existing Agent query and selected
   helper through the adapter. Characterize identical original fingerprints, including
   PostgreSQL timestamp boundaries, ordinary admissions and original historical receipts.
3. Prove missing/corrupt/cross-admission evidence denial, transaction rollback, idempotent
   acceptance, old accepted rows, old prepared rows accepted after migration, downgrade
   refusal, and cascade cleanup on the reused isolated PostgreSQL fixture. Retain query
   SQL/EXPLAIN showing no version-1 full payload and exact composite index use. Exercise
   actual registered source/publication/history and the unchanged composition root.
4. Review scoped architecture/dependencies and old-query simplification; build affected
   projects in serialized Release, confirm exact discovery, enforce full portability and
   retain frozen source/runtime/resource evidence. Repeat real width/payload query costs
   before unlocking producer work. Workflow adaptation, legacy backfill and sustained
   multi-client capacity require their own recorded proof; neither is implicitly green.

No unit-test timing SLA is inferred from this machine. Allocation and payload invariance
for unrelated width/prompts are structural acceptance criteria; actual SQL latency,
acceptance-write cost, retained storage growth and concurrent workload remain measured.
