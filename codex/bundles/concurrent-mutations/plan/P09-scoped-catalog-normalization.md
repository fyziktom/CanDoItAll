# P09: scoped reuse of unchanged catalog normalization

Status: implemented with measured allocation reduction; [qualification remains open](../validation/2026-10-21-scoped-catalog-normalization.md) for an approval timeout and isolated latency tail.

## Evidence and decision

The [P08 record](../validation/2026-10-21-review-read-boundary.md) has 67 passing
correctness cases and six failing isolated HTTP latency cases. Each GET reads the
catalog four times. Normalizing the default catalog allocates 13.1 MB and takes a
238.7 ms median in the component diagnostic; seed construction is included.

Use one memo within each existing file-store instance. Do not implement the draft
global seed cache. Most repeated work occurs within one request; avoiding three
normalizations is the smaller first repair. Cross-request reuse would require global
eviction, single-flight initialization and additional invalidation coordination.
Measure this slice before adding those responsibilities.

## Responsibility and dependency direction

Persistence continues to depend on Core/Models and its existing infrastructure
contracts. No project reference, DI lifetime or API contract changes.

- Extend the existing bounded JSON snapshot cache to deeply frozen catalogs. It still
  reads and hashes the complete current file on every access. No timestamp shortcut.
- A new cohesive catalog-read normalizer owns unchanged-result reuse and the existing
  revision-independent payload comparison. Remove that comparison from the large
  store; keep persistence, locking and publication in their current owners.
- A separate seed-input reader captures the exact bytes consumed by existing template
  parsers. Pass its text-reading delegate explicitly through the two pack loaders,
  seed builder and inline capability materializer. Do not use ambient mutable state.
- The existing normalizer gains a seeded overload; its ordinary callers keep the
  original behavior. There is no cached authorization decision or execution runtime.

CodeAnalytics baseline `snap-20261021203734-f5942be3` loads 47 documents, 136 types
and 1,454 members in the Persistence scope, with 52 findings and one existing type
cycle. The store has 157 members, seed normalizer 77, seed builder 27 and snapshot
cache 14. The scoped inventory excludes outside project edges; the actual project
file has ten references and will be compared unchanged. Twelve diagnostics include
existing NuGet audit errors and truncated diagram exports. Direct affected builds
are required; the snapshot is orientation, not a successful build claim.

## Current-input and failure contract

Composition inspection found that the real host always supplies
`IAgentCatalogMutationPolicy`; its reads therefore take the locked path. Apply the
memo to the pure normalization inside catalog save preparation as well. Remove the
earlier duplicate normalization in `LoadNormalizedCatalogCoreAsync`, whose callee
already normalizes before applying the policy. Keep policy invocation, invariant
validation, metadata preservation, comparison and publication on every locked read.
Test a policy denial after warming the same store. The optimization cannot bypass
current Project-access policy even when raw catalog and template inputs are identical.

The memo key is the exact raw immutable catalog instance returned after current-byte
validation. Reuse also requires the same resolved Agent/Capability roots and complete
content identity for every file consumed by the successful seed build: manifests,
team/member JSON, instructions, capability and policy JSON, inline instructions and
resources. A new manifest reference invalidates through the manifest itself. Capture
the digest from the bytes actually decoded, including BOM handling.

Compare content, not mtime or length. Missing/denied inputs invalidate reuse, after
which the ordinary loader determines the authoritative result or error. Never return
the old catalog on a failed fresh normalization. A failure is not retained. A changed
catalog follows the existing locked normalization/publication path; unassigned
catalogs cannot be reused. Current manager/profile/source and Agent/Project checks
still execute on every read.

Cache only payload-equivalent results; use the existing full serialization comparison
with the data revision cleared. Do not introduce hand-maintained field equality.
Freeze the returned normalized collections before retention. The existing raw cache
keeps its 16-entry / 64 MiB estimate. The one extra memo accepts only raw entries with
an estimated weight at most 8 MiB. Its file witness is bounded to 512 files and 8 MiB
of consumed input. Oversized material is normalized normally without memo retention.
The memo retains both raw and normalized references; it may keep raw data alive after
raw-cache eviction. It can therefore retain roughly two eligible catalog graphs in
addition to the raw cache, not just one 8 MiB graph. These are retention estimates,
not a promise about peak managed heap usage.

Publish the complete memo atomically. Concurrent cold reads may repeat normalization;
they do not serialize through a new global lock or share mutable loaders. They retain
at most one completed memo per store. File validation precedes each use, and no cache
hit substitutes for an existing publication fence or authority lease.

Disable normalization reuse when assembly updates are enabled, using
[MetadataUpdater.IsSupported](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata.metadataupdater.issupported?view=net-10.0).
This prevents a long-lived development scope from retaining normalization produced by
old code. Raw data caching remains content-validated.

## Test and acceptance contract

Isolated owner tests use normalization/current-input delegates and cannot construct
the store or runtime. Cover unchanged reuse, raw identity changes, input changes,
publication-required and unassigned results, failures then repair, retention bounds,
immutable returned collections and concurrent readers. Isolated file-input tests use
streams and roots to cover equal-length content changes, root changes, deletion,
access denial, BOMs, duplicate changing reads and oversized witnesses.

Real-store tests cover current-byte invalidation, same-size/same-timestamp catalog
revocation, deletion/recreation and scope separation; template integration verifies
the real builder captures both loaders and materializer assets. Run the existing
snapshot-cache, seed/materialization, catalog publication and authority topics, plus
the 67 P08 correctness cases. State and confirm discovery counts before execution.

Build Persistence directly, refresh standard Release Unit/Integration/Web outputs
serially, run the full portability scan and final no-write enforcement, rendering
register and documentation checks. Compare scoped dependency/cycle evidence and the
actual project file. Repeat the identical six isolated HTTP shapes without relaxing
the one-second maximum or provider/child/Project assertions. Retain failed attempts.
Sustained concurrency with background recovery remains a later capacity gate.

## Resource and closure boundaries

Reuse the existing isolated PostgreSQL fixture and standard outputs. Record ownership
before creating the next evidence directory. Preserve the ordinary 5032 instance and
historical cleanup-denied resources. SPA remains on `codex/concurrent-mutations`.
This slice does not activate Process/Workflow producers or close the overall bundle.

## Follow-up after measured qualification

Retain all A/B/C failures and complete samples. Add bounded request-only diagnostics
to the unchanged actual-host approval test before selecting another repair. Existing
POST timeout, background workers and effect/accounting assertions stay unchanged.
Nested telemetry durations are inclusive; no payload or credential content is logged.
The checkpoint may be committed as measured unfinished work; dependent activation
remains gated. The separate accounting-recovery draft is not applied yet.

## Measured follow-up: seed cost visibility

The four original approval/restart cases pass with bounded request tracing (12.4–12.9 seconds for approval); their earlier 30-second failures remain evidence. A measured six-shape repeat still fails StepSnapshot/1024 at 1,250 ms with visible CPU below 13%. The next three-case profile records only 18–43 ms median for five full JSON comparisons, so comparison removal alone does not explain the wall-time cost.

Before changing the memo or authority checks, add activities to the existing normalizer and seed-input reader: normalization total, seed construction, pure normalization, input validation, memo hit and consumed-file count. Use the existing AgentFramework ActivitySource with numeric/Boolean tags only; no file paths, payloads or identity data. This is observability within Persistence, with no new project, lifetime, cache, lock or behavior. With no listener, StartActivity returns null. Existing bounded request diagnostics collect at most 4,096 spans.

Refresh standard Release Persistence, Web and Integration, scan all protected source, and execute the three existing profiled GET scenarios (1-step RunSnapshot, 1/1024-step StepSnapshot). Preserve all samples, constant-query/no-write/no-runtime assertions and the earlier failed acceptance evidence. Select a functional optimization only after these timings identify its target. The one-second acceptance and current-authority checks stay unchanged.

The seed diagnostic is now [retained](../validation/2026-10-21-scoped-catalog-normalization.md): three profiled cases pass, normalization totals 58–105 ms. Preserve seed validation; measure request construction/current authority next. The independent accounting implementation may proceed without producer activation. Finite and sustained capacity remain open.
