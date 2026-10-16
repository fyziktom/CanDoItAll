# Workspace defaults: first owner cutover refinement

Status: source-reviewed implementation preparation on 2026-10-16. No Workspace source,
build, discovery or execution changes. This refines W1 in `P05-owner-waves.md`; it does
not expand W1 into provider, secret, file-preference or workflow-settings ownership.

## Scope and source evidence

The six business defaults have three production persistence paths:

| Path | Current source | Required participation |
| --- | --- | --- |
| Ordinary read/replacement | `src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs:57`, `:85` | Coherent original snapshot, mandatory original head expectation, exact accepted result. Native and HTTP call this owner. |
| Provider bootstrap | `Services/WorkspaceDefaultsBootstrapService.cs:23` | Select the same current head; create or repair only the invalid provider preference under owner exclusion; retain all unrelated fields. |
| Provider-preference transfer | `DatabaseTransfer/WorkspaceDefaultProviderDatabaseTransferHandler.cs:31` | Existing target transaction and table exclusion; update only the preference and advance the head revision when it actually changes. |

The source sweep of `WorkspaceSettings`, `Workspace_Settings` and its saves, excluding
migrations/build outputs, found no fourth production business-default writer. The
similarly named Voice, Workflow and Floating Agent settings are different owners.
Tests directly seed settings in several provider/transfer fixtures; these must explicitly
seed a current head after the migration rather than preserve timestamp selection.

CodeAnalytics snapshots `snap-20261016170356-f6369a37` and
`snap-20261013111901-96f090ae` do not contain the Workspace project. Those negative
inventory responses are scope limitations, not absence-of-owner or graph proof. This
refinement uses the actual project/source files and does not build a competing snapshot
while the parent owns validation outputs.

## Minimal contracts and responsibility split

Keep the mutable six-field `WorkspaceSettingsModel` as the editor's input model. Add
owner-local immutable records in Workspace.Contracts:

- `WorkspaceSettingsValues`: the six submitted scalar values, captured before awaits.
- `WorkspaceSettingsHead`: validated nonempty row ID and content revision.
- `WorkspaceSettingsExpectation`: required dataset namespace and an explicit Empty or
  Existing state; Existing requires a head and Empty forbids one. Omitted/null wire
  expectation is invalid, never an implicit create permission.
- `WorkspaceSettingsSnapshot`: expectation plus exact normalized values.
- `ReplaceWorkspaceSettingsCommand`: original expectation plus frozen values.
- `WorkspaceSettingsSaveOutcome`: Accepted with exact snapshot/optional secondary
  diagnostic, Rejected with typed reason, or Unconfirmed. A rejected attempt does not
  establish the result of an earlier ambiguous dispatch.

Use the existing `DatasetNamespaceId` from SharedKernel. Workspace.Contracts currently
references only Security.Abstractions, which itself has no SharedKernel reference; this
requires **one direct inward reference** to the leaf SharedKernel project. It does not
require a new project or a Workspace.Contracts reference to Infrastructure/Projects.
This corrects the earlier W1 wording that no new reference was necessary. Verify the
evaluated changed-project graph at implementation; source project files show no reverse
edge from SharedKernel.

Add one cohesive `WorkspaceSettingsWriter` to own capture/normalization, the ordinary
transaction and accepted outcome. Keep a small internal head-persistence helper for the
three real writers: current-head query, owner table exclusion, creation and revision
advance. It accepts an existing WorkspaceSettingsDbContext and never commits another
owner's transaction. `WorkspaceService` delegates; do not add another partial or a
universal settings writer. The original storage/secret methods stay with their current
owners. The native IWorkspaceDefaultsOwner switches to the typed snapshot/command.

A read returns the namespace and current head from one coherent database observation,
including the explicit empty result. A single SQL projection joining the active
lifecycle row and optional current settings row is sufficient; an independently read
namespace followed by a normal settings query is not. Check the captured active profile
binding against that observation. The reader copies values and releases its connection;
it does not retain a transaction for an editing session.

The existing common SettingsWriteResult/ledger also serves secrets and file preferences.
Do not broaden those protocols accidentally. Translate the new defaults outcome in the
defaults controller/owner adapter; other owner contracts can remain unchanged.

## Current head and transaction order

Retain the W1 `IsCurrent` marker and unique partial index. Migration chooses one existing
head by `UpdatedAtUtc DESC, Id ASC`, marks all other rows inactive and preserves every
business field. Assign nonempty revision values without rewriting timestamps. Empty
tables remain empty until explicit bootstrap/create. All three writers and transfer
preview/export select IsCurrent; timestamps become audit data only. A second current
head is a database invariant violation, not a reason to silently choose one.

Ordinary save captures the active dataset binding before BEGIN. Use a short Read
Committed transaction with finite transaction/statement/lock timeouts; then acquire
lifecycle SHARE, then `LOCK TABLE "Workspace_Settings" IN SHARE ROW EXCLUSIVE MODE`,
then read the current head. This is one singleton aggregate, so table-level owner
serialization is appropriate. No provider locks, network calls, logging or activity
writes occur while that settings lock is held. Compare the original namespace/head/
revision under the lock and retain EF's original-revision concurrency predicate.

An ordinary accepted replacement advances revision even when normalization produces
identical values. Bootstrap and preference-transfer maintenance advance it only when
they change the preference or create a head. This keeps repeated bootstrap idempotent
and prevents a no-op provider transfer from invalidating every open defaults editor.
Every actual maintenance change invalidates old editor expectations.

Transfer already acquires the lifecycle utility table exclusion before its first
snapshot-producing target query in DatabaseTransferOperationRunner; its owner table
lock follows. Preserve that Serializable target / RepeatableRead source contract. Do
not add a second nested ordinary transaction or move a settings lock ahead of lifecycle.

### Bootstrap correction required before coding

`ProviderDefaultsBootstrapService.PrepareAsync:39` begins Read Committed and calls
PrepareProvidersAsync before returning its stage. It reads and tracks provider entities;
`ProviderDefaultsBootstrapStage.CommitAsync:39` saves those entities after the enlisted
Workspace participant has saved. Composition wires these at
`RuntimeHostServiceCollectionExtensions.cs:787`.

Acquiring lifecycle SHARE only inside WorkspaceDefaultsBootstrapService is too late:
bootstrap could read provider entities, wait while a target transfer replaces provider
data, then save the old tracked entities after the transfer. Capture the exact active
binding before PrepareAsync's transaction and acquire lifecycle SHARE immediately after
its BEGIN, before PrepareProvidersAsync. Use the existing Infrastructure activation
service and DatasetLifecycleStore static owner-context guard. ProviderManagement already
references Infrastructure; this needs no dependency on Workspace. The coordinated
transaction still owns provider + preference atomicity. The Workspace participant then
takes the settings table lock before reading its head. Keep the existing providerExists
callback a same-transaction database read, not an external provider call.

The separate preceding secret-bootstrap effect is not made atomic by this change and
must not be advertised as part of Workspace's accepted result. Provider health/config
writers remain separate P05 owners.

## Exact result, adapter and policy boundaries

`WorkspaceSettingsApi.SaveAsync:61` currently returns a later GetSettingsAsync result.
Return the accepted snapshot directly instead. A subsequent concurrent save must never
replace the acknowledgement of this request. Add the mandatory expectation to strict
PUT binding, 409 for actual stale head/known transaction rejection, and preserve current
read/write capabilities. HTTP provider existence/enabled prevalidation remains a
preflight check; ordinary settings save does not lock another owner's provider rows.

Native WorkspaceDefaultsDraft already tracks per-field edit versions. Store the
original expectation independently from the mutable model. On Accepted, always advance
the base expectation to the accepted one, while applying normalized field values only
where the captured field versions still match. On conflict preserve the draft and show
original/current values with explicit reload/reapply. Never refresh the expectation and
silently resubmit the same six-field replacement. Unknown freezes further saves; a
current settings read cannot establish whether that prior command committed.

Controller `SaveAsync` currently wraps acknowledgement, reconciliation and RefreshAsync
in one catch that marks the receipt Unknown. Separate those phases: after Accepted,
local mapping/notification/provider-refresh failure remains a known accepted result
requiring review. Keep the accepted snapshot before calling fallible presentation code.
Activity/currency publication follows commit and cannot change the accepted result.
CurrencyDisplayState is a process-local observation cache, not a database-currentness
certificate or a source for runtime policy admission.

CAS + exact acknowledgement is W1a/W1b's enabled-path cutover. Durable lost-response
replay remains explicit W1c work: owner-local Workspace receipts/epoch state, caller
binding, stable versions, disclosure and activation participation. Do not call Projects'
operation protocol merely because Workspace already has a Projects module reference.
Reuse reviewed Infrastructure lifecycle/transaction utilities and proven protocol rules;
extract shared neutral mechanics only when both concrete owners demonstrate identical
requirements. W1a/W1b cannot claim restart-safe replay before W1c closes.

For P07, a Workspace-owned policy defaults adapter reads the persisted typed policy and
head revision at admission. Runtime consumes a neutral narrow read contract beside its
policy vocabulary, wired by Composition, not WorkspaceService or the settings UI. Add
the default override only after this owner cutover and the P04/P07 resolver are ready;
copy/serialize it through every path above. Existing runs retain their admitted resolved
policy and source revision. Workspace preferences do not relax owner mandates, select
fresh data during an executor retry or themselves implement durable pause/replan.

## Focused proof and implementation order

1. Implement contracts, current-head mapping/migration, head helper, ordinary writer,
   bootstrap and transfer participation together. Retained-data migration covers empty,
   one row, multiple rows and timestamp ties; restart retains the same chosen head and
   all inactive rows. Owner/canonical model parity must remain exact.
2. Cut native/API/sandbox/generated client callers over before enabling the new writer.
   No compatibility overload may reread a current revision to fill a missing original.
   Check the sibling API client's generated GET/PUT contracts rather than inventing a
   new SPA settings screen.
3. Real PostgreSQL barriers prove two readers/one accepted save, two Empty readers/one
   created head, bootstrap versus human provider selection, bootstrap waiting behind
   transfer before any provider read, and transfer versus ordinary save in both orders.
   Assert original/accepted/current values and revision, not just row counts.
4. Inject precommit rollback and uncertain commit acknowledgement separately. Known
   activity/logger failures retain Accepted. Inject controller reconciliation failure
   after Accepted; its receipt must never become Unknown. Verify current-read failure
   cannot replay a write and later local typing survives acknowledgement.
5. Adapt existing WorkspaceSettingsOwnerPersistenceTests, WorkspacePreferenceTransferOwnerTests,
   BusinessBootstrapOwnerIntegrationTests, WorkspaceCoreOwnerOutcomeTests,
   WorkspaceSettingsHttpOutcomeTests, ApiSectionPermissionsTests and Workspace UI draft/
   receipt tests. Direct provider fixtures must mark their seeded current head explicitly.
   Use existing leased PostgreSQL and standard serialized build outputs; no new fixture.

Measure small singleton save under 8/32 writers for bounded refusal/lock time and compare
read latency during a held writer. High write concurrency to this one aggregate is
expected to serialize; unrelated Projects writes must not acquire its table lock.
No whole-engine throughput claim follows from this low-volume owner. W1c and P07 need
their own receipt/recovery and runtime-policy proofs before overall phase closure.
