# W1b: current Workspace settings head and caller cutover

Status: source preparation authorized on 2026-10-16 after the owner inventory below.
This implements `P05-workspace-first-owner-refinement.md`, using W1a's existing
immutable contracts. No W1b build, test, migration execution or client generation has
run. Main remains frozen; edits belong to the managed continuation checkout. W1c
durable recovery and P07 runtime policy activation remain separate gates.

## Exact ownership and dependency boundary

| Owner / files | Change |
| --- | --- |
| Workspace `Models/WorkspaceModels.cs`, `Persistence/WorkspaceSettingsDbContext.cs` | Add `IsCurrent` and application-managed concurrency token to the stored row. Existing context stamping already supports `IHasConcurrencyToken`. Keep value-only reads for non-editor consumers, but select only the current row. Delegate conditional saves to the new writer. |
| New Workspace `Persistence/WorkspaceSettingsHeadStore.cs` | Same-context current-head query, owner exclusion and field/revision mutation helpers. No transaction ownership, provider calls, activity or generic mutation callback. |
| New Workspace `Services/WorkspaceSettingsWriter.cs` | Coherent namespace/head acquisition and conditional replacement. Own ordinary transaction, exact acknowledgement and postcommit diagnostics. |
| Workspace `Services/WorkspaceDefaultsBootstrapService.cs` | Select/create the current head in the existing provider transaction. Lock before reading; repair only an invalid preference. An unchanged bootstrap does not advance revision or timestamp. |
| ProviderManagement `Administration/ProviderDefaultsBootstrapService.cs` and `ProviderDefaultsBootstrapStage.cs` | Capture the binding for the explicit bootstrap profile, acquire lifecycle SHARE immediately after BEGIN and before the first provider query, and release both transaction/context on failure. Preserve known commit knowledge. |
| Workspace `DatabaseTransfer/WorkspaceDefaultProviderDatabaseTransferHandler.cs` | Source/target/preview select the current head. Reuse the existing transfer transaction and target exclusion. Same preference is a no-op; a real change advances revision while retaining other fields. |
| Workspace.Contracts `WorkspaceSettingsMutationContracts.cs`, `WorkspaceOwnerContracts.cs` | Switch only `IWorkspaceDefaultsOwner` to snapshot/command/outcome. Add `OwnerRetired` rejection and `CleanupPending` secondary diagnostic. Other settings protocols remain unchanged. |
| Workspace `Presentation/WorkspaceDefaultsOwner.cs`, `Services/WorkspaceModuleServiceCollectionExtensions.cs` | Wire the owner writer. Keep `IsCurrent` as a process/profile lifetime hint; it never certifies the stored head. Remove the tokenless save forwarding path. |
| Presentation `WorkspaceDefaultsController.cs`; UI `WorkspaceDefaultsState.cs`, `WorkspaceDefaultsSurface.razor` | Retain original expectation, frozen submission and field versions. Preserve known acceptance before fallible reconciliation/notification. Add explicit comparison/reload/reconcile without replacing dirty input silently. |
| Workspace `Pages/SettingsPage.razor[.cs]`; sandbox `WorkspaceScenarioStore.cs` | Preserve owner retirement and late-completion isolation. Supply typed sandbox snapshots/outcomes and conflict scenarios. Update only affected rendering-register entries. |
| Web `Api/WorkspaceSettingsApi.cs` and narrow owner-specific metadata if necessary | GET returns coherent snapshot; PUT requires original expectation plus all six values. Return the exact accepted snapshot directly. Remove post-save GET as acknowledgement. Preserve existing read/write capability checks. |
| Canonical PostgreSQL migration and model snapshot | Parent serializes generation with C2. No hand-generated competing model or migration and no independently executed EF command. |
| SPA existing `SettingsWorkspacePage.vue`, narrow Workspace settings session/API adapter, actual generated GET/PUT DTOs | Separate original expectation, submission and later typing; preserve the form on errors. Use the reviewed single-attempt transport without changing the global client. Source changes wait for reviewed actual emitted schema and explicit SPA release. |

No new project is required. W1a already added the direct Workspace.Contracts to
SharedKernel reference for `DatasetNamespaceId`. ProviderManagement already references
Infrastructure, so its lifecycle correction adds no Workspace dependency. Composition
already coordinates providers and Workspace using one database transaction. Workspace
must not import Projects operation types, H2 authority decisions or C2 receipt state.
The provider-existence callback remains a same-transaction database read in bootstrap;
ordinary save's enabled-provider preflight stays outside the Workspace transaction.

## Persistence and compatibility decisions

Migration chooses exactly one retained row by `UpdatedAtUtc DESC, Id ASC`, marks it
current, supplies nonempty revisions to all rows, and preserves all business fields and
timestamps. Empty tables remain empty. A unique partial index allows at most one true
`IsCurrent`; multiple current rows are corruption, never a timestamp fallback. Use
`SingleOrDefault` in all three writers and relevant reads. `IsCurrent` is a storage
marker; `IWorkspaceDefaultsOwner.IsCurrent` is a different lifetime predicate.

This requires stopped old hosts during migration/cutover. Old timestamp-based writers
cannot participate safely alongside the new marker/token protocol. There is no dual
read/write fallback and no overload that invents a missing original expectation. The
entity/migration, three writers, native/API/sandbox and SPA are one release perimeter,
even when authored and reviewed in smaller coherent subsets.

Ordinary replacement: validate/freeze all six bounded values before awaits; capture
active binding before BEGIN; short Read Committed transaction; lifecycle SHARE;
`Workspace_Settings` SHARE ROW EXCLUSIVE; current-head query; compare namespace plus
Empty/Existing and exact ID/revision; one flush; commit. Preserve the original EF
concurrency predicate. An accepted ordinary no-op still advances revision. No provider
enumeration, external effect, activity or logging belongs under that settings lock.
Use finite transaction/statement/lock budgets following the existing utility patterns;
record exact selected values in source/proof rather than treating them as measured SLAs.

Snapshot acquisition uses a short Read Committed lifecycle SHARE transaction and one
current-head query, with no settings writer lock. It returns a namespace and head from
the same protected observation, including explicit Empty. It never retains a transaction
for the editor lifetime. Production protected acquisition/replacement requires
PostgreSQL. Preserve the already explicit InMemory bootstrap/transfer fixtures, but do
not manufacture a production dataset namespace to satisfy them.

The retained-data migration does not normalize historical business text. New saves
enforce the existing public maxima (name 200, output 40, currency 3, culture 40, notes
8192), reject invalid nonblank currency/culture, and apply documented trimming/defaults
consistently in the owner. A bounded HTTP request reader must account for escaped JSON
overhead independently of these character limits. Legacy oversized reads fail visibly
at a declared client boundary; they are not truncated or silently written back. The
exact body cap and oversized-read proof are implementation review items before enabling
the HTTP client, not permission to copy unbounded collections.

Transfer retains source RepeatableRead and target Serializable. Existing
`DatabaseTransferOperationRunner` acquires target lifecycle exclusion before the first
snapshot-producing query and before owner table exclusion. The head helper must not
start another transaction. Bootstrap acquires lifecycle SHARE before provider reads,
then settings exclusion before settings reads; provider and settings flushes remain in
that transaction. Its preceding secret bootstrap is a separate effect.

## Exact outcomes and editor behavior

`ReadSnapshotAsync(CancellationToken)` returns `WorkspaceSettingsSnapshot`;
`SaveSettingsAsync(ReplaceWorkspaceSettingsCommand, CancellationToken)` returns
`WorkspaceSettingsSaveOutcome`. Value-only `GetSettingsAsync` remains a read adapter.
`IWorkspaceDefaultsOwner.ReadAsync` and `SaveAsync` use those same typed shapes.

`OwnerRetired` is a known refusal before dispatch by the retired native owner, not a
claim that the database namespace changed. `CleanupPending` is legal only after actual
commit acknowledgement. Set accepted snapshot/commit knowledge before disposal,
currency publication, activity, logging, presentation callbacks or refresh. A disposal
failure must not erase an earlier primary error, skip another owned resource, or turn
an acknowledged commit into Unconfirmed. A commit call that throws without proof
remains Unconfirmed; W1b has no receipt that could resolve it.

On acceptance, advance the editor's base expectation and reconcile only fields whose
versions still equal the frozen submission's versions. Preserve post-dispatch typing.
Keep exact accepted data before invoking callbacks. A retired controller records its
own known outcome but cannot modify its successor. On HeadChanged, retain the original
draft and expectation; a separate current read populates comparison only. Explicit
discard/reload starts a new draft; explicit reconciliation starts from the current
snapshot while keeping the prior local values reachable for copy. Neither action is a
hidden retry. Unknown blocks another save from that origin. Current GET is an
observation, never proof of the unknown attempt. Navigation/restart durability remains
W1c's unfulfilled requirement.

HTTP: accepted 200 contains the exact snapshot and optional secondary diagnostic;
invalid values/shape 400; known head/dataset/retired-context/transaction refusal 409;
Unconfirmed 503 is typed and distinguishable from generic predispatch server failure.
Keep 401/403 behavior. Missing/null/duplicate/malformed expectation must fail binding
deterministically, including constructor failures. Validate emitted nullable head and
closed outcome schemas, not just DTO reflection. The provider check is a preflight, not
an atomic provider/workspace invariant. Request-admission authorization is preserved;
W1b does not claim H2 post-wait authority fencing or atomic file-to-SQL revocation.

SPA `SettingsWorkspacePage.vue` currently replaces live input after response and hides
the form on error. Its replacement must freeze a typed six-field command before await,
track per-field edit versions, preserve the opaque provider reference even though the
screen has no provider picker, and fence late reads after route disposal. Use a dedicated
client with the reviewed JSON transport and SDK retries disabled, not global retry
configuration. Generated operation options currently fail to forward cancellation;
the reviewed narrow generator fix is required. No Projects IDB records, operation IDs
or epochs are reused for Workspace. No generated DTOs are invented before wire proof.

## Proof inventory and resource plan

Existing source inventory (not new execution evidence): W1a contracts 15 rows;
`WorkspaceSettingsOwnerPersistenceTests` 2; `WorkspacePreferenceTransferOwnerTests` 4;
`BusinessBootstrapOwnerIntegrationTests` 12; `WorkspaceCoreOwnerOutcomeTests` 10;
`WorkspaceSettingsHttpOutcomeTests` 1; `ApiSectionPermissionsTests` 3.
Workspace UI draft/receipt/refresh/history-shell files have 19/3/4/9 rows;
`WorkspaceSettingsBrowserTests` has 2. Recount changed source before discovery; variants
inside a Fact are not extra runtime cases. Do not present these inventory numbers as
proof that W1b passed.

The first two API permission Facts currently use the actual defaults handlers through
an InMemory host. Move those defaults-permitted arrangements to the existing isolated
PostgreSQL fixture, keeping the unrelated workflow Fact on its existing host. Do not add
a production fallback owner/namespace just to preserve this fixture. Direct settings
seeds in provider/history/scheduler/transfer tests must explicitly identify a current
head. The old owner test that selects a future timestamp is replaced by retained-data
migration proof, not preserved as a current-head rule. Activity fault injection must
target the new writer's dependency, not the old facade constructor.

Prospective unique proof arrangements, before authored-source counts are frozen:

1. Migration: empty, single, multiple and tied timestamps; rerun/restart preserves the
   chosen head, inactive rows, fields and timestamps; owner/canonical model parity.
2. PostgreSQL owner: two readers/one winner; two Empty readers/one create; stale
   namespace after reactivation; original revision preserved in the SQL predicate;
   accepted normalized no-op advances; rollback leaves no changes; actual commit then
   lost acknowledgement is Unconfirmed; acknowledged commit then cleanup/activity
   failure remains Accepted with the exact snapshot.
3. Bootstrap/transfer: concurrent bootstrap versus human provider choice; provider
   bootstrap blocked by lifecycle exclusion before its first provider SELECT; both
   participant flushes roll back together; preference transfer in both orders against
   ordinary save; unchanged bootstrap/transfer preserves revision and other fields.
4. HTTP: strict original expectation and shape; stale 409; exact accepted snapshot
   despite a later writer; distinguish typed unknown from known rejection; emitted
   schemas/real generated roundtrip and actual single-attempt cancellation path.
5. Native/session: later typing, read completion after retirement, known acceptance
   followed by reconciliation failure, explicit conflict reload/reconcile, and current
   read after unknown cannot unlock save. Browser proof uses two contexts and keeps
   primary editor and recovery controls readable at the existing desktop size.

Use existing leased PostgreSQL and standard serialized outputs; root owns execution,
canonical migration generation, source-derived discovery counts and portability gate.
No new database stack, build configuration or global resource cleanup is proposed.

The authored-source checkpoint is recorded in
[W1b source inventory](../validation/P05-W1b-current-head-source.md). It supersedes the
historical counts above only for the named changed classes: new owner concurrency 10
Facts, new lightweight controller/draft 5 Facts, HTTP outcomes 5 Facts (previously 1),
and browser settings 3 Facts (previously 2). None has been executed for W1b. Migration
backfill, deliberate bootstrap/save and transfer/save overlap, the performance matrix,
and actual generated SPA wire/lifecycle proof remain open; the authored cases must not
be described as coverage of those arrangements.

## Risks, performance and remaining enablement gates

The singleton writer necessarily serializes. Measure 1/8/32 hot writers with bounded
success/conflict/timeout counts and p50/p95/p99 wait/total latency. Concurrent current-head
reads must remain compatible with a held writer; unrelated Projects writes must not
acquire the Workspace lock. Count SQL/allocations: one head query and one flush for an
ordinary update, no provider catalog scan under the lock. Include bootstrap blocked by
transfer and cancellation cleanup; do not label this a whole-engine throughput result.

The process currency cache can lag or receive completions out of order. It remains a
presentation observation, never runtime policy/current-head authority. No new cache
coordination framework is part of W1b.

Before enablement: parent confirms W1a validation, serializes the Workspace/C2 canonical
model snapshot, reviews exact disposal/unknown classification, and proves actual HTTP
schema plus SPA generated/session cutover. Backend compilation alone does not close the
caller perimeter. W1c must add retained receipts and restart recovery before claiming
end-to-end durable mutation recovery for Workspace.
