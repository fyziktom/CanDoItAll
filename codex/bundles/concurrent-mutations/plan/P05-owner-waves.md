# P05: remaining owner waves

Status: implementation plan from source inspection on 2026-10-16. No production edit,
build, test discovery, execution, performance result or owner closure is claimed. Read
with architecture05 and `P04-P07-runtime-policy.md`. Paths are repository-relative;
`../CanDoItAll.SPA` is the sibling client. This plan orders work, not a generic framework.

## Sequence and common completion rule

Start with **Workspace business defaults**, then **Resource registry metadata** in a
separate commit/card. Workspace affects every session, has a small six-field aggregate,
and is a prerequisite for reliable P07 policy defaults. Resources exposes a relatively
small native editor and two concrete creation paths; it offers an independent check of
owner/result semantics without adding an API. **TestLab is a third independent card**:
its root and three collections require coherent reads, identity mapping and an explicit
decision about editable run records. Do not combine these owners behind a shared CRUD
service, use Projects' definition revision as their content revision, or treat the three
cards as engine-wide completion.

Each owner closes its ordinary save/delete/enable intent, original read and all enabled
adapters, plus bootstrap/import/transfer/background writers. A successful owner result
must contain its exact normalized accepted fields and newly assigned identities; a
later current read cannot acknowledge an earlier submission. Preserve local edits made
after submission and freeze further writes after unknown outcomes. Reconciliation reads
are distinct from acknowledgement. Durable retry promises additionally depend on P02b
and lifecycle06: exact receipt, trusted caller/dataset/epoch address, finite retention,
current receipt-disclosure authorization and managed restore fencing. CAS safety may
ship as a named earlier subcard, but it does not imply lost-response replay is complete.

Use owner-local validated expectation/command/result records in existing Contracts
projects. Snapshot mutable collections at admission; no service rereads the target
revision just before save to fill a missing original expectation. Map malformed/missing
expectations to validation and actual stale content to typed conflict. Known PostgreSQL
rollback with unchanged owner revision is transaction conflict, not an invented content
change. No automatic replay of semantic writes. Preserve current lifetime, source,
profile-generation and permission checks independently.

The reuse of infrastructure transaction machinery is already available. No new
production project or reference is necessary for the first three CAS cards. Contracts
remain free of EF; UI and Presentation call owner contracts. Workspace.Contracts currently
references Security.Abstractions, Resources.Contracts references Projects.Contracts,
SharedKernel and FileTools integration abstractions, and TestLab.Contracts references
Projects.Contracts. Do not add reverse module dependencies or resource/provider SDKs
to the shared contracts. The reused infrastructure snapshot
`snap-20261016142537-4d8bda80` covers four projects/224 documents and reports 205 diagnostics;
it is limited source-orientation evidence, not a healthy graph or evidence that these
owners are absent. Direct project/source reads determine this plan. Obtain the healthy
evaluated changed-project graph before adding references and at integration review.

## Card W1 — Workspace defaults

### Complete observed perimeter

| Surface | Source and observed behavior | Required change/proof |
| --- | --- | --- |
| Canonical entity/read/save | `src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs:13`, `:57`, `:85`: Get selects latest timestamp or transient defaults; Save copies the request, selects latest row, overwrites fields and timestamp. No original content predicate. | Explicit current settings head and owner revision; read returns exact original expectation; Save validates it at the write boundary and returns exact accepted snapshot. |
| Native owner/draft/controller | `Presentation/WorkspaceDefaultsOwner.cs:5` captures profile/generation; `src/Modules/CanDoItAll.Modules.Workspace.Contracts/WorkspaceSettingsModel.cs:6`, `:26` model/copy; `src/UI/CanDoItAll.Workspace.UI/WorkspaceDefaultsState.cs:8` tracks local field versions; `src/Modules/CanDoItAll.Modules.Workspace.Presentation/WorkspaceDefaultsController.cs:70` admits/submits/reconciles. | Copy and retain original expectation with immutable submission. Advance base only from exact accepted snapshot, preserve post-submit edits, explicit conflict/reload/reapply and terminal unknown state. Retired owner cannot dispatch into another profile. |
| Native renderer/host | `src/UI/CanDoItAll.Workspace.UI/WorkspaceDefaultsSurface.razor`, module Settings page and existing operation ledger. | Render conflict and original/current review without silently replacing draft. Follow UI seam ownership and query Components MCP before UI changes. |
| HTTP | `src/App/CanDoItAll.Web/Api/WorkspaceSettingsApi.cs:11`, `:24`, `:28`, `:53`: strict six-field PUT DTO, scopes, provider existence precheck; success rereads current settings at `:61`. | Add required read expectation to replacement request, typed 409 response, preserve scopes. Return `saved` acknowledgement directly; a concurrent second save must never replace it. Postcommit warning header/result remains truthful. Do not call a preflight provider check an atomic guarantee that provider configuration cannot change. |
| SPA/client/API skill | `../CanDoItAll.SPA/packages/api-client/src/api/canDoItAllClientOperations.ts:816`, `:838` has generated GET/PUT; inspected application search found no direct WorkspaceSettings consumer. | Regenerate schema/client types using actual sibling conventions and prove wire token forwarding. Do not invent a settings screen solely for this card. Check runtime callers/generated client references again at freeze; update maintained API/skill documentation through its owning repository only if within authorized scope. |
| Bootstrap writer | Module `Services/WorkspaceDefaultsBootstrapService.cs:23`: enlisted context creates a default row or repairs missing provider preference using `FirstOrDefault`; provider stage is prepared in `Modules.AgentFramework.ProviderManagement/Administration/ProviderDefaultsBootstrapService.cs:PrepareAsync` under ReadCommitted. | Use the same current-head selection and owner exclusion; advance revision only for an actual preference change. Preserve every other field. Repair only if the current preference still satisfies the bootstrap predicate under its transaction; a human selection wins when no longer invalid. Never create a second head or overwrite a six-field draft. |
| Database transfer writer/export | Module `DatabaseTransfer/WorkspaceDefaultProviderDatabaseTransferHandler.cs:31` uses actual source snapshot and target transaction, locks target tables, copies only opaque provider ID into latest row. Its Preview and source read export only that preference. | Preserve source RepeatableRead/target Serializable session contract. Transfer changes only current target preference and revision. It is an explicit maintenance intent, not a read-then-replace editor command; ordinary drafts become stale. Preserve all other legacy rows and fields. |
| Profile/restore and read side effect | `WorkspaceModuleServiceCollectionExtensions.cs:21` binds owner factory to canonical profile; Get/Save update `CurrencyDisplayState`; full database backup/restore is control-plane work. | Dataset/profile fencing follows06. Currency display/activity run after authoritative commit and must not turn accepted result into retryable failure; current read can update display as observation, not revise an old receipt. |
| Agent/workflow tools and health | Module registrations and call-site sweep found no separate first-party defaults mutation tool or health writer; public route is HTTP and native owner, with the two infrastructure writers above. | Repeat route/registry/call-site sweep at freeze. Do not add a tool merely to satisfy an inventory cell. Future P07 policy settings use this closed owner. Workflow/Voice settings with similarly named methods are different owners, listed below. |

### Smallest safe storage/transaction design

The existing table permits multiple legacy rows and chooses a head by timestamp. Do
not delete those rows, silently merge them, or continue choosing the active record from
a clock that can move backwards. Add a persisted `IsCurrent` marker with a PostgreSQL
unique filtered index allowing at most one true row. Migration marks the existing
latest `UpdatedAtUtc` row current, with deterministic ID ordering for timestamp ties,
and retains every other row as inactive legacy data. Document that tie rule; previously
tied selection was unspecified. New empty datasets have no head until explicit create
or bootstrap. All production reads/writers select the marker; legacy fixture setup
must exercise migration or set the current marker deliberately.

Add nonempty application-managed revision to the current row, with database concurrency
predicate. `WorkspaceSettingsExpectation` represents either a specifically observed
empty scope or the exact head ID/revision in the captured dataset/profile. Do not use
null to mean both “caller omitted the expectation” and “caller observed empty.”
The absence/create race needs owner serialization as well as the unique index; an
ordinary update never upserts a deleted/replaced head.

Use one short owner lock for the singleton scope. Minimal concrete choice: take the
same PostgreSQL `SHARE ROW EXCLUSIVE` lock on `Workspace_Settings` before selecting or
creating the head in every ordinary save and bootstrap participant; transfer already
takes this mode through `DatabaseTransferOwnerSessionRunner.AcquireTargetTableLocksAsync`.
Ordinary owner save owns a short transaction; bootstrap/transfer enlist the existing
transaction, never start a nested one or downgrade its isolation. This table represents
one logical settings aggregate per database, so serialization is appropriate; ordinary
reads remain possible. Do not also introduce oppositely ordered advisory locks. Review
provider-bootstrap acquisition order before implementation, since it may already have
provider writes. Normal defaults save must not acquire provider row locks after this
lock merely to repeat HTTP prevalidation. Measure lock duration and never perform
external provider calls, activity writes or UI waits under it.

Inside the lock require original empty/head state, assign normalized scalar fields,
advance the revision, save and produce exact acknowledgement. Index/EF conflict maps
to typed no-effect outcome. Commit then update display/activity with a known-commit
warning path that preserves the exact saved snapshot even if logger/display/activity
fails. An accepted acknowledgement plus local reconciliation failure is committed with
review required, not unknown. P02b follow-up adds receipt atomically inside this same
owner transaction; do not persist whole settings/LLM histories for convenience.

Implementation units: W1a entity/migration/current-head helper and all three writers;
W1b native/API/client expectation/ack/conflict loop; W1c durable replay once06/P02b ready.
W1a and W1b form one enabled-path cutover: do not leave a legacy PUT or native save that
can omit expectations. A new tiny owner persistence helper is justified by the three
real writers; a universal settings abstraction or new WorkspaceService partial is not.

Affected production builds: Workspace.Contracts, Workspace.UI, Workspace.Presentation,
Workspace module, AgentFramework.ProviderManagement bootstrap consumer, PostgreSql
migrations, Composition and Web; Workspace.UiSandbox when its scenario contract changes.
The sibling API client is a separate typed consumer check. Source schema changes reopen
current model parity and bootstrap/transfer tests. Wider provider configuration changes
remain a separate owner card.

## Card R1 — Resource metadata

### Observed perimeter

| Surface | Source/behavior | Closure |
| --- | --- | --- |
| Entity, editor read and save | `src/Modules/CanDoItAll.Modules.Resources/ResourceModels.cs:12`, `:190`, `:226`: single root, plugin-normalized config, project admission and current binding guard under serializable resource/project scopes. | Add owner metadata expectation/revision; Get returns it with normalized editable fields; capture command deep-copies `Configuration`; compare exact original root before overwrite. Keep project source/target binding checks separate. |
| Delete | Same file `:334`: current binding/lifetime checked, no original content expectation; missing row returns. | Existing-item delete must carry original metadata expectation so a stale editor cannot delete a newer configuration. Define missing/already-deleted result explicitly; exact replay belongs to receipt lookup. Deletion must not delete external file bytes. |
| Native contract/presentation | `Resources.Contracts/ResourceModels.cs:78` editor/Capture; `ResourceRegistryOwner.cs:30`, `:38`; `Resources.Presentation/ResourceRegistryController.cs:325`, `:343`; UI `ResourceRegistryWorkspace.cs:24`. | Save/delete command has exact expectation. Replace `Result<Guid>` save response with exact metadata acknowledgement; controller currently reads latest GetAsync for reconciliation and must stop using it as accepted state. Preserve origin/mutation/field/config versions and pending/unknown guards. |
| Creation by file promotion | `ResourceStorageObjectPromotion.cs:64`, `:98`, `:105`: validates external activation separately, then creates one registry row or returns existing identical governed config under project scope. | New row initializes metadata revision. Existing promotion is explicit idempotent create/find; it must not overwrite existing name/sensitivity or refresh its revision unnecessarily. Receipt distinguishes created/reused. Source authorization/file activation remains outside metadata transaction. |
| Projection/binding reads | ResourceModels `ListProjectProjectionFacts*` and `ReadProjectionScopeAsync`; Workbench `ProjectStructureAssemblyService.cs:872`, `ProjectNodes/ProjectNodeScopeBridge.cs`; `ResourceProjectionFacts.cs`. | Preserve exact lifetime filtering and enlisted owner query. Display projections are not editor original snapshots. Delayed search/projection writeback must be fenced by accepted metadata revision as well as project lifetime or be conditional/outbox-derived. |
| Memory/read tools | `ResourceSourceSnapshotProvider.cs`, `ResourceMemorySourceGatewayAdapter.cs`, `ResourcesAgentChatContextBuilder.cs`, `ResourceFileToolsStorageBindingSource.cs`. | These expose/read resource sources, not an ordinary metadata write API. Preserve read authorization and stable resource binding. Metadata token does not prove external content unchanged. |
| Open/browse external files | `ResourceStorageObjectInteraction.cs:16` opens read-only known-file session after current source checks; `ResourceBrowseOwner`, `ResourceFileBrowseCoordinator`, connector plugins. | No metadata edit token may authorize external bytes. Keep source/file revision and capability protocols separate. |
| Import/export/health/transfer | `ResourcesModuleServiceCollectionExtensions.cs` registers owner, promotion writer and read adapters. `ResourcesProjectTransferTargetStateParticipant.cs:10`, `:23` only declares table lock/residue detection; it is not a resource transfer writer. No separate resource metadata HTTP route, agent mutation provider, import/export or background health write was found by entity/table/service and registry searches. | Recheck enabled routes/registrations at freeze; generic database restore obeys06. `ValidationStatus` is currently ordinary editor data plus promotion initialization; no invented health worker. Any later automated validation must condition on captured config revision and change only observation fields. |

Choose an owner-local `ResourceMetadataExpectation` containing resource ID, original
nonempty revision and captured profile/dataset context; target ProjectWriteAdmission
remains explicit. Rebinding to another selected project cannot replace the original
resource expectation. Keep existing sorted resource+old/new project mutation keys and
source admission. Validate original EF token after acquiring the current owner scope;
no network/plugin execution under transaction. Connector `ValidateEditor`/serialization
are deterministic local preparation; don't allow a new connector to insert external I/O
into that path. Metadata receipt stores normalized config and opaque secret references,
never secret values, file content or unrelated context. Apply the same bounds to request
and exact result; freeze max config/description bytes before enabling durable receipts.

All observed resource writes are Save, Delete and promotion creation. Dataset restoration
is separately fenced; target-residue participant intentionally blocks unsupported whole
project transfer into a nonempty resource owner. Do not “complete the inventory” by
adding new metadata APIs or export writers. Add revision model mapping to runtime and
maintenance model consistently, preserve legacy connector/config data and null legacy
project lifetimes, and initialize a fresh revision on imported/recreated rows.

Known-commit exception currently retains only resource ID; extend it/result to retain
the exact accepted acknowledgement. Search/activity failures after commit must not
trigger a second write, and later current read must not acknowledge that prior mutation.
Resource controller already separates committed-warning from unknown: preserve this
distinction when adding typed conflicts. Add an explicit Conflict state/review action
where a generic Refused currently leaves the same stale command freely retryable.

Affected builds: Resources.Contracts, Resources.UI, Resources.Presentation, Resources
module, migrations, Workbench read consumers, Composition/Web and Resources.UiSandbox.
No new dependency is needed; no external driver change belongs in R1. R1a CAS+all adapters
can proceed independently after W1 architecture acceptance; R1b exact durable replay
depends on06/P02b and bounded config retention.

## Card T1 — TestLab aggregate

The physical model is root `TestPlan` plus `TestCaseRecord`, `TestEvidenceRecord` and
`TestRunRecord`. `TestLabModels.cs:264` replaces all four in one serializable owner
transaction; `:307–309` synchronizes every collection and `:385` removes missing child
rows. `GetAsync` at `:217` reads root/runs/cases/evidence in separate queries without a
coherent snapshot. `TestLabContracts.cs` initializes missing collections to empty.
`SyncCollectionAsync` presently treats an unknown submitted child ID as a new child.
These are aggregate correctness issues, not fixed by a token only on the editor.

Architecture05's general observation that execution results are history must not be
read as a claim that these `TestRunRecord` rows are already append-only: the current UI
allows editing them. Preserve that product behavior in the first protective subcard.
Protect the complete current user-editable aggregate with one original revision and
coherent snapshot; this makes a stale full replacement reject rather than erase newer
runs. No automated append-only run writer was found in the current production entity
call-site sweep. Before adding such a writer, split its append/receipt semantics and
read-only execution history from editable plan definition; never let a definition save
silently delete automated execution history. Do not remove current manual run editing
or pretend it is immutable as an incidental concurrency refactor.

| Surface | Exact seam | Required conversion |
| --- | --- | --- |
| Owner read/save | `TestLabModels.cs:217`, `:264`, `:385`; `Persistence/TestLabDbContext.cs`. | Coherent bounded RepeatableRead aggregate snapshot with owner revision; deep immutable replacement command; root revision rotates on scalar or child change. Validate duplicate/foreign/missing existing child IDs and explicit collections before mutation. |
| Native owner/session | `Workspace/TestLabWorkspaceOwner.cs:30`; `TestLabWorkspaceSession.cs:155`, `:240` returns only ID then rereads latest for reconciliation. | Exact accepted root/children/ID mapping result, typed conflict/no-effect state and known-commit warning carrying same result. Mark acceptance before local reconciliation. Current Get may refresh list but never act as exact acknowledgement. |
| Contract/draft | `Modules.TestLab.Contracts/TestLabContracts.cs`; `src/UI/CanDoItAll.TestLab.UI/TestLabDraft.cs`, `TestLabSubmission.cs:126`. | Preserve original expectation through Clone/submission; immutable base plus local row identities/field changes. Do not fill current revision in Save. Unknown retains submitted command; conflict retains user draft and blocks accidental same-intent replay. |
| Projection/list | `ListAsync`, `ListProjectProjectionFacts*`, `ReadProjectionScopeAsync`; Workbench `ProjectStructureAssemblyService.cs:941`. | Keep global plans supported and retained orphan lifetimes readable; current binding remains separate from metadata revision. Avoid loading all run records for every list if changed query can cheaply select latest per plan; a broad unrelated list refactor is not required for CAS. |
| Project change/delete | Save explicitly supports global or selected project; typed `TestPlanBindingChangedException`; observed owner has no ordinary delete method. | Original aggregate expectation survives project target change; validate old and new binding in existing ordered keys. Do not add delete merely for parity with Resources. Retired project data cannot adopt a recreated project's lifetime automatically. |
| HTTP/tool/import/export/runner | Module registration has owner/service and residue inspector. App API and runtime provider searches found no dedicated TestLab definition mutation endpoint/tool. `TestLabProjectTransferTargetStateParticipant` is read-only residue detection. Scenario stores/browser fixture are controlled test writers. | Preserve actual native surface. Repeat production registry/entity/table sweep at freeze. Fixture seed must initialize valid revision/head semantics explicitly; no production maintenance fallback to an unconditioned edit. Database restore is06. Evidence `ArtifactPath` is metadata, not authority to overwrite file bytes. |

Keep `test-plan:<id>` and old/new project keys; same-owner lock/transaction wraps root,
children, revision and receipt. Original read must use the owner API, not a list token
or original timestamp invented at Save. Use exact normalized accepted child rows and
client row-key mapping for new children; avoid mutating the submitted object after commit
as the only identity acknowledgement. Full replacement requires finite total child/byte
caps and request-level duplicate-ID rejection. If a future HTTP adapter is added,
missing/null collections are invalid, not silently default-empty.

T1a closes current aggregate read/write/child identity and native acknowledgement. T1b
adds durable replay/retention and only then a runner-specific append contract if an
actual enabled writer requires it. The latter is a distinct domain card, not assumed
necessary code for the initial native stale-edit fix. Affected builds: TestLab.Contracts,
TestLab.UI, TestLab module, migrations, Workbench projection consumers, Composition/Web,
TestLab.UiSandbox and the actual TestLab browser fixture when its contracts change.

## First-wave proof and resource plan

Each card uses existing Debug/Release outputs and the already isolated PostgreSQL18
fixture; parent serializes shared-output builds. No new Docker stack, custom configuration
or duplicate output tree. Owner native/UI changes follow the relevant `docs/testing.md`
slice and Components/UI seams. Build-backed discovery must confirm every focused count
before execution. Source-derived current counts below are not executed counts:

| Selection in Integration project | Current source cases | Reason |
| --- | ---: | --- |
| `WorkspaceSettingsOwnerPersistenceTests` | 2 | Mapping, legacy data, restart/profile isolation. |
| `WorkspaceSettingsHttpOutcomeTests` | 1 | Exact saved six-field result after activity failure. |
| `WorkspacePreferenceTransferOwnerTests` | 4 | Source snapshot, target rollback, opaque preference transfer. |
| `WorkspaceCoreOwnerOutcomeTests` | 10 | Existing settings/related owner postcommit outcome contract; inspect selected coverage, do not weaken others. |
| `ResourcesOwnerPersistenceTests` | 3 | Mapping, config/owner references, source limits. |
| `ResourceTestLabAdmissionIntegrationTests` | 10 | Mixed owner lifetime, binding, delayed-save and in-flight capture assertions. Keep the whole bounded class as testing.md requires. |
| `OwnerPostcommitPersistenceTests` | 16 | Search/activity/cancellation and identity truth for Resource/TestLab. |
| `TestLabOwnerPersistenceTests` | 3 | Aggregate ID preservation, mapping and profiles. |

Use exact `FullyQualifiedName~<topic>` OR filters in
`tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`.
W1 regression set above is17 cases; R1's owner+mixed+postcommit set29; T1's
owner+mixed+postcommit set29. Run common cases once for the final combined candidate,
and rerun only when invalidated. `ResourceRegistryTests` in
`tests/Components/CanDoItAll.Resources.UI.Tests` currently has20 source cases (13 Facts,
7 InlineData); `TestLabSessionTests` in `tests/Unit/CanDoItAll.TestLab.Tests` has32
(15 Facts,17 InlineData). Workspace uses `tests/Components/CanDoItAll.Workspace.UI.Tests`;
freeze its exact changed topic/count when editing. Existing broad class source counts
must be refreshed if tests change.

Minimum new deterministic topics, each method intended as one Fact unless a writer
matrix deliberately expands it:

| Topic / count | Cases |
| --- | --- |
| `WorkspaceDefaultsConcurrencyTests` — 10 | `SecondStaleEditorIsRejected`; `ConcurrentEmptyScopeCreatesOneHead`; `MissingExpectationCannotUpdate`; `BootstrapInvalidatesPriorDraft`; `BootstrapCannotRevertNewHumanSelection`; `TransferInvalidatesOnlyTargetPreferenceDraft`; `LegacyHeadMigrationRetainsInactiveRows`; `ClockRollbackCannotSelectAnotherHead`; `LateReadCannotReplaceAcceptedSnapshot`; `AcceptedResultSurvivesDisplayActivityAndLoggingFailure` (expand injected fault rows explicitly). |
| `ResourceMetadataConcurrencyTests` — 8 | `SequentialStaleReplacementIsRejected`; `MissingExpectationIsRejected`; `StaleDeletePreservesNewConfiguration`; `RebindRetainsOriginalResourceExpectation`; `PromotionReuseDoesNotOverwriteMetadata`; `RecreatedResourceRejectsOldRevision`; `DelayedSearchCannotOverwriteNewerProjection`; `ExactAcknowledgementPreservesLocalPostSubmitEdits`. |
| `TestPlanConcurrencyTests` — 9 | `CoherentReadCannotMixRootAndChildren`; `StaleSaveCannotRemoveNewRun`; `ChildOnlyChangeInvalidatesOldAggregate`; `DuplicateChildIdsAreRejected`; `ForeignChildIdIsRejected`; `NullReplacementCollectionIsRejectedBeforeTransaction`; `GlobalPlanRetainsOriginalExpectation`; `ExactChildAcknowledgementSurvivesDelayedRefresh`; `FailureBeforeCommitLeavesAllCollectionsUnchanged`. |
| `RemainingOwnerReceiptTests` — at least3 owner cases | `LostReplyResolvesExactAcceptedOutcome` for defaults/resource/testplan, expanded independently for mismatch/reuse, expiry, access revocation and dataset retirement once P02b is available. A read of current state is not a passing receipt test. |

The first three new topics are27 minimum proposed cases, not discovery promises; injected
fault/writer matrices may expand them. Add one real two-session browser journey per
native owner: load A/B, save A, reject B without draft loss, explicitly compare/reapply
against a newly loaded base, confirm no silent overwrite and correct accepted values.
Use existing WorkspaceSettingsBrowserTests, Resources browser/scenario host and
TestLabBrowserTests at supported large-desktop viewport. Include normal editor and
conflict/review overlay screenshots; keep notification failure and delayed read paths.

Use barrier-controlled interleavings, independent contexts/processes for database races,
and fixed clock for equal/backward timestamps. No sleeps as conflict proof. Migrations
prove legacy seed, upgrade, fresh-create and model parity; restore/import rotates dataset
namespace per06. Run portability-static for every protected-source card, review all
added/stale findings, refresh only intentional deltas, then final enforcement without
write-baseline. Shared migration/contract changes trigger owning broader topics and
the bundle's named frozen release gate, not an unfiltered full test launch per edit.

## Later ordered waves and complete-engine ledger

The order is dependency-driven; independent owner cards may run in parallel after
review. No row marked guarded is automatically done. A new route, importer, background
worker, health path, callback or synchronization writer reopens that owner's perimeter.

| Wave | Owners from architecture05 | Required before closing |
| --- | --- | --- |
| W1/R1/T1 | Workspace defaults, Resource metadata, TestLab editable aggregate | Cards above plus exact enabled adapters/maintenance writers and durable outcome obligations. |
| E1 — execution configuration | Technical Agent definition; Agent teams; capability definition/assignment; Workflow definition authoring; provider profiles/defaults | Close optional original expectations on ordinary existing-object saves and health/source-sync field isolation. Preserve immutable Workflow versions and Process/runtime claims. This wave supplies P07 settings safety; each owner is a separate card. |
| B1 — business planning facts | CRM account/directory relationships; HR workforce/profile/skills/capacity/staffing/AI business profile; project party assignments/rates | Root content plus complete-set/overlap witnesses, every directory merge/import/batch/capacity writer. Protect rates/availability under owner transaction ports before P04 can claim them material-current. Opportunity/affiliation/assignment guarded paths receive separate proof below. |
| G1 — Workbench remaining perimeter | Graph nodes/links/metadata/placement; canonical task details/schedule/assignment/pricing/execution; planning outputs | Existing original-state semantics retained; native/API/tool/process/workflow/import/move/compensation writers inventoried. P04 bounded basis/publisher and P07 policies remain separately gated. Avoid replacing precise field semantics with one project token. |
| S1 — scheduling and collaboration | Scheduler plan definition/enable/delete; Collaboration thread/inbox metadata/read status and message append | Definition expectations across scheduler/tool/UI, fire identity separately; idempotent message append/intent-specific read-status updates without global transcript conflicts. Retained history is not mutable definition. |
| I1 — local/external infrastructure | Storage catalog/routing/health, Data Sources profile catalog/activation/import, Secrets metadata/rotation | Separate human config from observations; original config preconditions plus actual file/vault/provider conditional protocol and exact external outcome reconciliation. Profile activation, offline restore and retirement follow06. Owner SQL token never protects external bytes. |
| X1 — integrations | Plugin grants/connections/install settings/OAuth; Memory provider profiles; Shared provider source/import/publication/sync | Configuration expectation for all writers; current authority on effect; callback/refresh cannot undo disconnect; retries obey provider idempotency. Keep disabled-by-default Memory behavior and owner boundaries. Shared provider guarded paths still need perimeter proof. |
| C1 — already conditional owners | Simple Chat definition/conversation metadata; Process authoring; Prompt Gallery draft/version/archives/membership/assets; Provider-history capture/retention; API account/password/scopes and API token lifecycle | Preserve their existing conditional/receipt protocols; prove every enabled adapter/secondary intent. Do not migrate correct owner protocols merely to make them look uniform. |
| F1 — final discovery | Runtime Workflow settings/Voice settings, floating-chat settings, workspace connector command definitions, custom Process definition factories, exported packages, personal/shared layout state, other registered API/tool/module mutable surfaces | Explicit classify/read/write inventory. `AgentVoiceService.SaveSettingsAsync` delegates Workflow settings and `FloatingAgentChatSettingsService.SaveSettingsAsync` is separate: neither is Workspace business defaults. Personal presentation state may have different semantics, but shared business configuration cannot disappear from scope because05 had no row. |

## Guarded owners: source-confirmed paths and remaining proof

| Owner/path | Confirmed source condition | Required closure evidence |
| --- | --- | --- |
| CRM opportunity | `CrmHrServices.cs:1447`, `:1454`, `:1629` requires/compares original timestamp and maps EF conflict; conversion `:1691`, `:1707` binds original value. | Sequential stale edit and conversion race through native/API/registered tool; no duplicate created target or compensation loss; other opportunity writers inventory. |
| CRM affiliations and project assignments | `PartyOrganizationAffiliationService.cs:91`, `:106` requires original row stamps; CrmHrServices assignment replacement compares saved set/snapshots under scopes. | Insert/delete/merge phantom and empty replacement; rate/role/move/duplicate-member cases; current project lifetime and original membership. Existing `PartyOrganizationAffiliationIntegrationTests`, `ProjectPartyAssignmentIntegrationTests`, `ProjectPartyAssignmentMoveReceiptIntegrationTests` are anchors, not passing claims. |
| Shared provider configuration | Source/import/publication services in05 require tokens on inspected paths. | Background source sync versus human edits, enable/delete versus in-flight refresh, publication version and received original token through actual adapters. `SharedProviderSourceSyncIntegrationTests` anchor. |
| Simple Chat | `EfLlmChatDefinitionRepository.cs:180` original token+definition revision SQL condition; `EfLlmChatConversationRepository.cs:38` original token predicate. | Actual UI/API token forwarding, create receipt duplicate/lost response, metadata versus transcript append/claim separation, missing tokens refused. `LlmChatTransactionalConcurrencyIntegrationTests` and create-receipt topics. |
| Process authoring | `EfProcessAuthoringStore` validates original/inherited head under ordered locks and stores operation/fingerprint outcome atomically with publication. | Adapter originals, same operation different payload, inherited/global/project overlay races, restart and legacy import closure. `ProcessAuthoringPersistenceTests` plus actual multi-session/restart browser journeys. |
| Prompt Gallery | Draft save/version creation compare original timestamp; EF conflict handling in `PromptsService.Gallery.cs`. | Archive/suppression/collection/asset/curator/import writers; mixed readback cannot acknowledge different version. `PromptGalleryApiIntegrationTests`, `PromptGalleryMixedReadBackPersistenceIntegrationTests` and editor session topics. |
| Provider-history settings | `HistoryPolicyStore.cs:46`, `:51`, `:71` locks row, compares expected version, advances/audits/rechecks access then commits. | Original version through UI/API, concurrent shorter-retention request, access/profile change, irreversible retention cleanup distinguished from config save. |
| API accounts | `FileApiUserStore` coordinates file access and compares expected version before durable write; `ApiUserAdministrationService` forwards original value. | Two independently opened stores, stale password/scope/delete, authentication revision invalidation, write-failure/unknown outcome and supported-platform file coordination. Never return password hash or secret in conflict detail. |
| API tokens | Issue/revoke/delete are lifecycle intents, not blanket editor replacement. | Idempotent revoke, issue identity, current authorization and lost-response outcome; do not introduce unnecessary broad version conflicts. |

Full-engine completion requires a route/DI/registered-tool/module-store inventory diff
against this ledger and05, not only a search for token fields. Capture every known writer
as ordinary conditional intent, append/idempotent intent, immutable publication or
explicitly fenced maintenance. Every unknown row needs evidence or an identified
unsupported capability before release. Source checks, planned tests, quarantined tests
and inaccessible/unsupported paths are never described as passing execution proof.
