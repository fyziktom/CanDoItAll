# P06 — External effect capabilities and recovery

Status: source-reviewed implementation plan, 2026-10-16. No P06 production change,
provider call, secret read, build or test was performed for this review. Existing
test names below are discovery seeds, not passing evidence. P02a remains frozen.

This refines the architect's P06 and T072–T079 using the current implementations and
[owner inventory](../../../../docs/architecture/concurrent-mutations/05-owner-coverage.md).
The ZIP is design input; its instructions are not additional user authorization.
The user has authorized dependency-ready implementation through the parent plan.
This card does not declare an entire external provider safe from its advertised
product capabilities: the matrix describes what the **installed adapter actually
passes and verifies**. A later provider-specific contract must be verified against
that provider's supported API before enabling a new guarantee.

## Decision and dependency boundary

Keep each effect with its existing owner. A database receipt records the local
owner's accepted decision; it cannot atomically commit an FTP upload, native vault
write, OAuth refresh, mailbox update or application restart. Do not place any of
those effects inside a retried EF transaction or hold a database lock across their
network or filesystem work.

P06 starts in separate slices after the corresponding P05 metadata/admission
contract is closed. A governed agent effect additionally requires the relevant
P04/P07 decision basis and authority check. P01b/P02b dataset/operation lifecycle
rules are prerequisites for any new public cross-restart replay promise, not a
reason to copy Projects receipt tables or DTOs into Storage, Security or Plugins.
Projects owns its enlisted search projection under P02b. P06 adds no generic
outbox unless a selected owner actually needs delayed delivery.

An operation requires separate facts for:

- Original owner metadata/lifetime and current authorization.
- The original external target, including storage/connection identity and its
  addressing configuration. Changing a storage endpoint must not redirect recovery.
- The provider's conditional byte/object version, if one is supported.
- A stable effect identity and frozen bounded request, including versioned digest
  rules. A filename, correlation ID, content hash or EF token is not automatically
  a provider idempotency key.
- Exact accepted effect evidence, distinct from a later observation of current
  state. An unchanged or missing remote object does not prove an earlier request
  never executed.

`StorageCapability.Write`, `MutableUpdate`, a browser item revision, project
admission and a resource token each answer different questions. Do not infer strict
conditional write or durable replay from any of them.

## Actual storage and file capabilities

Source prefix `Storage/` below is
`src/Foundation/CanDoItAll.Infrastructure/Storage/`. The current provider enum in
`Storage/Models/StorageModels.cs` contains FileSystem, Ipfs and Ftp only.

| Family / current entry | Conditional write and identity observed | Replay, unknown outcome and compensation observed | Required P06 behavior |
| --- | --- | --- | --- |
| Filesystem ordinary placement: `Drivers/FileSystemStorageDriver.cs`, `SaveAsync` | No hint uses allocated name plus durable `CreateNew`. Explicit `RelativePathHint` takes replacement options without an original revision. Identity is storage/root/relative path, not a file incarnation. | No operation lookup in the ordinary driver. A complete replacement can precede an exception from later hardening/observation. `DeleteAsync` is unconditional for the path. | Existing mutable targets require the selected protected mode below. Keep creation separate from replacement; a missing expected object cannot silently become create. Do not use ordinary hinted placement as a protected edit. |
| Filesystem revisioned replacement: same file, `GetRevisionAsync` / `ReplaceAsync`; `Abstractions/StorageRevisionedContentContracts.cs` | `CreateRevision` hashes **length and LastWriteTimeUtc ticks**, not content. A `beforeCommit` comparison runs while `DurableFileWriter` holds its sidecar-file coordination lease. That coordinates participants using that exact lease; independent editors do not participate. Null expectation plus `AllowOverwrite` permits unconditional replacement. | No effect receipt. Accepted revision is read **after** the durable writer returns and releases its lease, so another cooperating writer can advance the path before this read. Timestamp/length restoration can also conceal a different file or A→B→A. | Do not advertise external-editor-safe CAS. Fix coherent read/accepted revision capture for any retained coordinated mode. A content digest alone still does not close check/replace races or incarnation ABA. Strict mode rejects targets without the required guarantee; offer immutable save-as/new version where appropriate. |
| Filesystem stable creation: `Drivers/FileSystemStorageDriver.StablePlacement.cs` | Intent-derived path by default, or the frozen explicit hint; `CreateNew` refuses an existing target. Driver declares `CanRecoverWithoutWriteAcknowledgement=true`. | Existing Storage owner records dispatch and verifies exact length/SHA-256 before completion. Reconciliation does not allocate a second filename or overwrite a mismatching target. This is staged creation, not conditional replacement. | Reuse the placement owner, target fingerprint and exact digest. Prefer owner-allocated immutable paths. An arbitrary hinted existing path is not adopted as evidence that this operation wrote it. Narrow cleanup requires ownership, current no-reference proof and no active dispatch. |
| FTP: `Drivers/FtpStorageDriver.cs`, `.StablePlacement.cs`, `FtpWebStorageTransport.cs`, `RemoteStorageTransportContracts.cs` | Upload sends FTP `UploadFile`; delete sends `DeleteFile`. Neither accepts original version, object incarnation, conditional create, provider operation ID or lookup token. Listing revision does not change this. Stable placement chooses a deterministic path but still uses upload. | Stable driver explicitly declares recovery without write acknowledgement **false**. Existing recovery requires acknowledgement or explicit operator verification that server-side dispatch stopped, followed by exact readback. Missing file delete is treated as done; it does not protect a recreated path. Cancellation aborts the client request, not proof of server rollback. | Reject protected overwrite/delete and strict conditional create with the current adapter. Preserve bounded staged-upload behavior with honest uncertainty and an exclusive operation-owned target assumption; do not upgrade it to universal exactly-once. Local SQL locks or a proxy cannot protect against other FTP clients. A different provider API may add real conditions later. |
| IPFS immutable add: `Drivers/IpfsStorageDriver.cs`, `.StablePlacement.cs`, `IpfsHttpStorageTransport.cs` | No mutable replacement or delete capability. Stable add fixes CID version, hash, chunking, raw leaves, metadata flags and `pin=false`; computes target with `only-hash=true`, then stores and verifies returned CID matches. Ordinary add uses fewer fixed options and is not the same protocol. | Stable owner can verify exact bytes at the prepared CID after lost acknowledgement. `CompleteStableTargetAsync` performs optional pinning. Pin failure can leave stored content and an uncertain local completion. Driver `DeleteAsync` throws `NotSupportedException`; no unpin owner exists here. | Use stable add for new immutable versions. Metadata pointer CAS controls which CID a resource references. Pin ownership/retention is a separate effect; do not claim rollback deletes content or unpin another owner's shared CID. MFS reads in the transport do not supply a mutable-write guarantee. |
| Native file editor: `src/Integration/CanDoItAll.FileTools.Integration/AuthorizedFileContentSource.cs`, `AuthorizedFileSaveTarget.cs` | Authorization uses a bound handle and current access context. Read opens a stream, then separately obtains the driver revision; an old open stream may receive a newer path revision. Save requires `IStorageRevisionedContentDriver`, maps conflicts, and separately authorizes overwrite when expectation is null. Payload is bounded. | Save returns a persisted revision, not a durable effect receipt. `ProcessLocalFileCatalogRevisionService` is an invalidation counter, not cross-process external authority. | Carry coherent content+version from one driver operation, preserve dirty content on conflict/unknown, and do not interpret missing expectation as an ordinary update. Keep explicit overwrite authorization separate from whether a required safety guarantee is available. A UI retry requires original frozen operation evidence. |
| Compatibility store and profile transfer: `Storage/WorkspaceStorage.cs` (`LocalFileStore.SaveTextAsync/SaveBytesAsync`), `Storage/Transfer/StorageProfileTransferService.cs` | Compatibility writes call ordinary filesystem Save with a fixed path hint. Transfer placement uses ordinary placement and selected-target deletion calls the raw driver. | These calls do not acquire stable placement identity merely because another caller does. Transfer's own admission/compensation rules still apply. | Inventory each mutable user-data caller before cutover. Migrate protected writes through the owner, or reject the protected operation. Generated unique artifacts can remain append-only under documented ownership. Preserve original target identity during rollback. |
| Agent/plugin workspace files: `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileMutationService.cs`, `WorkspaceFileService.cs`, `PluginWorkspaceFiles.cs`; `...Maf/Runtime/Workspace/WorkspaceFilesystemRuntimePlugin.cs` | Write/append/copy/move/delete have path containment and staged-file logic, but no submitted content revision. `overwrite` is a boolean. Append captures and rechecks a preparation-time fingerprint before replacement; this is not the agent's original read condition or an atomic condition against independent writers. Copy/move involve source and destination. This is a separate path from Storage's revisioned driver. | Workspace receipts/results describe work and cleanup warnings; they are not an original-state CAS or proof of deduplicated replay. Staged rename and backups do not protect independent edits. | Include all six mutation kinds in the selected file slice. Protect source identity, destination existence/version and directory membership where required. Stop blind append/replay; prefer append-item identity for an owned append stream. Shell/process execution capable of arbitrary writes remains outside a file-CAS guarantee unless separately constrained. |

`src/Foundation/CanDoItAll.Infrastructure/FileSystem/DurableFileWriter.cs` is worth
reusing: safe path revalidation, sibling temporary files, flush, bounded sidecar
coordination, create-new versus replacement and cleanup are already implemented.
It is not a cross-resource transaction or a native conditional replace against
noncooperating writers. The sidecar uses `FileShare.None`; filesystem/platform
semantics and aliases still need cross-process proof on the supported deployment.

### Concrete filesystem choice before source changes

Use immutable content placement plus an owner-controlled logical reference as the
first strong supported edit mode for managed resource content. Prepare a new blob
with a stable Storage intent, verify it, then CAS the resource's original metadata
revision to that exact blob reference and commit the owner receipt. A failed owner
CAS retains an unattached blob for guarded cleanup; it never restores an old
mutable path. This guarantees the logical resource version only. It must be
presented as a new managed version, not a claim that arbitrary physical files are
immutable or protected from other operating-system users.

For an existing shared physical path, keep strict conditional update unavailable
until a real provider condition exists. A coordinated-owned-root mode is possible
only if every mutator uses the owner protocol, permissions prevent bypass, aliases
resolve to the same identity, and durable incarnation/version state is committed
with the effect or recoverable journal. Merely checking a configuration checkbox,
adding a SQL row token, or taking a local mutex does not establish those facts.
If that mode is selected, its exact durable layout and crash protocol require a
small separate work card before implementation; do not invent two independently
replaced sidecar/content files and call them atomic. Do not silently migrate an
existing shared path into owned storage.

## Metadata and resource authority

`src/Modules/CanDoItAll.Modules.Resources/ResourceModels.cs` owns name, location,
connector config and secret references; `ResourceRegistryOwner.cs` owns its editor
flow. `ResourceConnectorPlugins.cs` describes/serializes connector configuration.
That is not a write to the referenced repository, folder, SQL service or remote
URL. `ResourceStorageObjectPromotion.cs` authorizes a storage selection and writes
a resource reference; promotion does not make the external bytes part of a SQL
transaction. `StorageObjectResourceConnectorPlugin.cs` and
`ResourceStorageObjectInteraction.cs` must continue resolving content through
Storage/FileTools authorization, not execute byte writes from the renderer.

The resource owner protects its original editable metadata token, project
admission, binding/lifetime and collection membership. Storage independently
protects the original storage config/addressing snapshot and actual content
capabilities. `Storage/Persistence/StorageCatalogService.cs` must split editable
configuration from health observation: a health result cannot replace a newer
credential/endpoint configuration, and an edit cannot erase newer health state
accidentally. Resource metadata success must not imply byte success. Conversely,
verified uploaded bytes do not imply the resource pointer was accepted.

Existing `Storage/Drivers/StorageObjectDeletionService.cs` validates caller binding
facts and marks a stable placement deleted before calling the driver's delete.
Retain those guards; marking the placement prevents recreation, but does not make
unconditional physical deletion safe after an external actor replaces the path.
Strict cleanup additionally requires an immutable owned object or supported
conditional delete. If that condition cannot be established, retain the object
and expose cleanup-required; do not delete on matching name or size alone.

## Vault and secret capability matrix

Source prefix `Security/` is `src/Modules/CanDoItAll.Modules.Security/`. No secret
value, resolved environment variable or live vault was read in this review.

| Family / entry | Actual condition, identity and recovery support | Required behavior / compensation limit |
| --- | --- | --- |
| `SecurityModels.cs`, `SecretService.SaveCoreAsync/SaveCapturedAsync` | Captures a draft copy; allocates a new version-like vault key for each attempt, writes the vault first, then updates metadata. No submitted original metadata token. DB exception becomes `SecretMutationUnknownException`; old-key cleanup/activity after metadata success have `SecretCommittedException` stage. | Preserve those commit distinctions. Add owner original metadata/lifetime and stable secret-effect identity before dispatch. Allocate the new vault key once per effect, not each retry. Verify/stage outside SQL, CAS pointer and receipt inside SQL, then guarded retired-key cleanup. Vault failure can itself have an unknown outcome; it must not invite a new-key blind resend. |
| DPAPI, external wrapping key file, Unix local-user file and development DataProtection file: `SecretVaults.cs`, `ExternalWrappingKeySecretVault.cs` | `ISecretVault.Set/Get/Delete` has no expected version or receipt. `FileBackedSecretVault` writes protected bytes to a hash-derived key path with durable file replacement/private mode; it overwrites a supplied key. Encryption/protection is not concurrency control. | Use unique owner-reserved keys and never mutate a published key for ordinary secret replacement. Exact key plus a secure private verification mechanism can reconcile staging; the metadata pointer is the authority. Do not put raw values or a public hash of low-entropy secret values into receipts. |
| macOS Keychain: `NativeSecretVaults.cs`, `MacOsKeychainClient` | Identity is service/account. Set first adds, then modifies an existing item on duplicate. Delete looks up and deletes; missing is treated as done. No provider CAS/idempotency/accepted-result lookup is exposed. | Treat only owner-exclusive reserved keys as stageable. A successful later lookup is current observation, not proof of request identity. Never restore/delete an old shared account key as compensation. |
| Linux Secret Service: same file, `LinuxSecretServiceVault` | Calls `secret-tool store/lookup/clear` for application+key. Input uses stdin; no conditional version or operation lookup. Process cancellation/timeout does not prove that the service did not store. | Keep uncertain stage with exact key/provider context. Do not retry a possibly executing process automatically or log its stdin/output. Clear only a proven retired owner key with no active reference. |
| `InMemorySecretVault` | Concurrent dictionary, overwrite/delete by key; development opt-in; no restart durability. | Use fakes for deterministic tests. Do not claim production recovery across restart. |
| `MauiSecureStorageVault`, `AzureKeyVaultSecretVault`, `HashiCorpSecretVault` | Current classes derive from `UnsupportedSecretVault`, whose operations throw. Provider enum names are not implemented capabilities. | Continue explicit unavailable/rejection. Do not invent a cloud-vault CAS contract or fallback to a weaker provider. |
| `SecretMigration.cs`, `SecretDatabaseTransferParticipant.cs` | Migration already owns a journal, deterministic destination keys, destination verification, source-reference transition, restart checkpoint and staged rollback. Its coordination is specific to migration; it is not the ordinary editor protocol. | Reuse its patterns and preserve its own original-reference predicates. Cross-check migration/transfer against live secret edits and new key retention before cutover. A resumed migration must not delete a key now referenced by a later accepted edit. |

`SecretService.DeleteAsync` commits metadata deletion with reference policies
before vault deletion and reports postcommit cleanup separately. Add original
secret lifetime/version to deletion; never turn vault cleanup failure into a claim
that metadata deletion rolled back. A staged key may only be cleaned after proving
that no accepted metadata pointer or in-flight authorized use owns it. Define the
existing consumer read/lease boundary before aggressive retired-key cleanup.
Security owns any protected pending payload storage. Do not persist secrets in the
Projects/browser pending-command store or expose them in receipts/status APIs.

## Plugin and remote action capability matrix

Source prefix `Plugins/` is `src/Modules/CanDoItAll.Modules.Plugins/`.

| Family / actual entry | Current guarantee and missing capability | P06 decision / honest recovery |
| --- | --- | --- |
| Grants, install enablement and connection settings: `Catalog/PluginPermissionServices.cs`, `PluginCatalogServices.cs` | Catalog/grant/connection writers load current rows and save without the original condition; some returned DTOs carry tokens. `PluginCommit` distinguishes committed logging failure. These are local metadata writes, not proof of remote revocation. | P05 adds exact owner conditions and durable accepted metadata result as needed. Recheck current grant/connection authority at dispatch. A revoked grant prevents future admitted dispatch; it cannot recall an already accepted remote action. |
| OAuth start/callback/refresh/disconnect: `OAuth/PluginOAuthService.cs` | Authorization code exchange/refresh posts to token endpoint without provider idempotency or lookup. Refresh is serialized by a static process-local semaphore. Callback and refresh write a connection-derived fixed token vault key. Disconnect deletes the vault key before marking SQL disconnected. No external revoke request is shown. | Replace fixed-key overwrite with per-attempt staged credential versions and an owner connection generation/tombstone. Claim one transition before dispatch; stale callback/refresh cannot publish after disconnect or reconnect. Do not hold a DB transaction during HTTP. Unknown exchange/refresh is retained/reconnect-required unless the provider supplies a documented safe resolution; blind retry can consume a rotated credential. Local disconnected is not provider-token revocation. |
| Plugin package extraction/install: `Catalog/PluginPackageServices.cs`, `PluginPackageManifestStore.ExtractInstalledPackageAsync` | Bounded validated archive, sibling staging directory, then old installed directory deletion and move into package-ID path. DB installation and restart marking are later stages; `PluginPackageStageException` reports progress. No original package generation or durable effect identity protects replacement. | Capture archive digest+package identity once. Stage an immutable version; CAS an owner installation pointer/generation and receipt. Do not delete an active previous directory before accepted switch. Cleanup only unused versions, accounting for loaded assemblies and other hosts. Stage progress survives unknown metadata result and must not trigger re-extraction under a fresh identity. |
| Application restart: `PluginRuntimeRestartService` in same file | Process-local semaphore guards requesting restart; status file writes are ordinary `File.WriteAllTextAsync`. A delayed task calls `StopApplication`; startup clears marker. PID is reported, not a durable process incarnation. | Record targeted runtime incarnation and accepted restart request before scheduling. Reconcile a restarted host without requesting another restart; do not replay a historical restart into a new process. No rollback of an already stopped application. Keep the owned status writer; use existing durable file primitives where needed. |
| Gmail labels: `src/plugins/Implementations/CanDoItAll.Plugin.Gmail/GmailApiClient.cs`, `MarkMessageProcessedAsync` | Resolves/possibly creates processed label, reads labels, then sends add/remove deltas. No original mailbox/message version or idempotency key is sent. Deltas avoid replacing unrelated labels, but a later user reversal makes blind replay non-equivalent. Label creation is another effect. | Declare a current-state label command only when that is the selected semantic; it is not an original-snapshot conditional edit. Strict expected-state actions are unavailable with this adapter. Persist stage identities/account/message/label IDs; timeout is uncertain, not automatic resend or inverse-label compensation. |
| Microsoft Graph categories: `src/plugins/Implementations/CanDoItAll.Plugin.Office365/Office365GraphClient.cs`, `MarkMessageProcessedAsync` | Ensures master category, reads message category list, then PATCHes the complete list. No `If-Match` or original remote version is sent. This can overwrite another actor's category change. | Block protected full-set replacement until the actual endpoint's conditional contract is verified and implemented. A local message lock does not protect remote users. Capture account/message identity and version coherently; retain category-creation and message-update outcomes separately. No restoring the old category list as compensation. |
| Docker pull/start: `src/plugins/Implementations/CanDoItAll.Plugin.Docker/DockerHostToolService.cs` | Reads inventory by container name; existing running container returns inspect, stopped container starts, absent container can pull and run. Name/tag and running-state observations are not immutable incarnation/configuration expectations. Command timeout can occur after the daemon acted. | Bind effect to daemon identity, operation-owned container identity/label, requested config digest and image digest where applicable. Read back exact container ID/config before acknowledging or cleanup. A same-named replacement or shared image is never stopped/deleted as compensation. No replay-safe claim from CLI exit status alone. |
| Generic package/runtime effects: `src/plugins/Abstractions/CanDoItAll.Plugins.Abstractions/PluginExecutionContracts.cs`; `src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Plugins/RuntimePackageWorkflowExecutor.cs` | HTTP contract allows POST/PUT/PATCH/DELETE with arbitrary headers; wrapper delegates inner executor. No universal provider idempotency, expected version or outcome lookup follows from these contracts. Concrete providers can also use their own HTTP clients as above. | Each admitted mutating capability must declare its real semantics and owner-specific recovery. A header named idempotency is only evidence after the recipient's contract validates it. Unknown third-party mutators cannot enter strict protected/replay-required execution. Read-only and explicitly weaker operations remain separately classified. |

These adapter observations do not assert what Gmail, Graph, Docker or a future
vault API could support. That requires provider-specific verification at the
implementation slice. No API exploration or remote mutation is necessary to
prove the current missing request conditions.

## Minimal effect lifecycle and compensation

Extend existing owner journals instead of creating a cross-module effect manager.
Storage already has `StorageStablePlacementService` and its Recovery,
Continuations and NativeContinuation parts, `StoragePlacementIntentRecord`,
`StoragePlacementRecoveryService` and owner continuation contracts. It freezes
bytes, fingerprints request/target, claims one dispatch, verifies bounded exact
readback and retains Completed/Uncertain/Conflict/Deleted outcomes. Keep its
original target, authorization and profile fencing. A retained receipt is the
historical accepted placement, not proof that a mutable target still contains it.

For a new owner effect, make these stages explicit in its typed state machine:

1. Validate/freeze bounded input, original owner condition, target and capability;
   record a stable intent before the first effect. No raw provider secrets in it.
2. Acquire a short owner claim and leave the database transaction before dispatch.
   Use the provider's real condition/idempotency key if available. A local claim
   suppresses participating duplicates; it does not cancel an old remote request.
3. Record the exact effect acknowledgement or Uncertain. Once dispatch may have
   started, cancellation is not Rejected. Preserve identity across retry/resume,
   and never create a new intent to escape uncertainty.
4. Reconcile with provider-specific evidence. Only supported readback/lookup can
   resolve uncertainty. A later precondition failure, missing object or expired
   receipt is not a general proof that the original effect did not happen.
5. Commit the owner reference/projection and accepted metadata receipt under the
   original conditions. An externally completed effect with rejected metadata is
   an orphan/cleanup case, not a successful user metadata save.
6. Perform separately recorded narrow cleanup. Capture exact created identity and
   expected current state; check no later owner references/use and no active
   dispatch. Delete only that owned object/version. If safe inverse is unsupported,
   retain evidence and require explicit recovery. Never restore a whole old snapshot.

Revocation overlapping dispatch has two honest results: rejection before dispatch,
or an already-dispatched action whose result must be reconciled without granting
new follow-on authority. Recheck source/runtime/owner identity before metadata
publication and before cleanup. A lease that expired while a remote request is
still running is not permission to issue a replacement request.

Retain exact terminal results through the published replay horizon, and compact
deduplication/tombstone evidence or explicitly refuse expired identities afterward.
Do not copy live effect histories into another dataset during transfer. Import of
an existing external reference is a distinct adoption with current authorization
and proof; it is not a replay of the source operation. Pending external effects
survive database restore independently: offline dataset reactivation alone cannot
undo a provider request. Old pending effects need fenced, original-target
reconciliation before new effects on that target; never dispatch them under a
fresh namespace automatically.

## Implementation slices and dependency checks

| Slice | Minimal edit set / owner | Entry and closure requirement |
| --- | --- | --- |
| P06a capability admission | Storage abstractions/driver facts, FileTools integration, agent workspace mutation contract and adapter; no UI-to-Infrastructure dependency | Typed capability verdict per operation including condition, identity, lookup and cleanup; unsupported protected operations rejected before dispatch. Existing flags remain usability facts. No shared generic execution engine. |
| P06b managed immutable content | Resources owner metadata contract plus existing Storage stable placement and continuation | Original resource CAS, stable new blob, exact receipt, conflict/unknown UI retention and narrow orphan cleanup. Coherent content/version read. Direct editor/tool/API/transfer entry points use the same owner. |
| P06c secret versions | Security metadata owner, vault staging/recovery, consumers and migration/transfer | Original metadata/lifetime; one key per effect; no plaintext pending-store leakage; interrupted write, metadata ambiguity and cleanup races. Existing unavailable providers remain unavailable. |
| P06d plugin settings and OAuth | Plugins metadata owner, OAuth transition owner, Security vault API | Concurrent callback/refresh/disconnect cannot publish stale credentials; unknown provider exchange is explicit. Cross-host coordination/claims tested, not just static semaphore behavior. |
| P06e package/restart | Existing Plugins package/restart services and composition | Immutable staged package switch, exact stage receipt and runtime incarnation; separate install/enable/restart commands. No silent old-directory deletion or restart replay. |
| P06f selected remote executor families | Gmail/Office365/Docker owners and governed workflow adapters | Actual provider contracts verified; unsupported strict operations rejected. Exact provider identity, bounded lookup and per-stage outcome passed into checkpoint recovery. Unknown plugins are not covered by a bundled-client test. |

These are closure slices, not permission for unrelated refactoring. The first
small P06a work card must choose exact public DTO/version/cutover and tests after
the relevant P05 owner contract; this capability matrix does not invent that
cross-owner contract in advance. FileTools sibling source remains read-only here.
If a required abstraction change belongs there, record a scoped sibling change
and compatible consumer migration before editing it.

## Deterministic proof and performance

Discover exact case counts after the implementation scope is fixed. Current
source seeds include `tests/Unit/CanDoItAll.Tests.Unit/StorageStablePlacementDriverTests.cs`,
`DurableFileWriterTests.cs`, `FileAccessAuthorizationTests.cs`,
`WorkspaceFileServiceTests.cs`, `WorkspaceFilesystemRuntimePluginTests.cs`,
`SecretVaultTests.cs`, `SecretMigrationTests.cs`, `PluginCapabilityFacadeTests.cs`;
and `tests/Integration/CanDoItAll.Tests.Integration/StorageStablePlacementPersistenceTests.cs`,
`ResourceStorageObjectIntegrationTests.cs`, `ResourcesOwnerPersistenceTests.cs`,
`SecretPortabilityIntegrationTests.cs`, `ProviderSecretTransferOwnerTests.cs`,
`PluginCatalogIntegrationTests.cs`, `PluginsOwnerPersistenceTests.cs`,
`EmailPluginClientTests.cs`. Preserve existing typed result/portability tests.

| Proof group | Deterministic arrangement and required assertion |
| --- | --- |
| T072 coherent file read/CAS | Barrier between stream open and revision observation; another writer replaces the path. Returned bytes and expectation must describe one version or read fails explicitly. Two original-version saves have one acceptance; accepted revision is captured before another writer advances. Same-length/restored-timestamp and A→B→A cases cannot falsely certify strict protection. Noncooperating path mode rejects unsupported strict writes. |
| T073/T074 accept then lose response | Transport fake commits to its private state then throws. Persisted intent is reused on restart. Filesystem/IPFS exact readback resolves only the configured mode; FTP without acknowledgement remains uncertain until dispatch termination is explicitly verified. New filename/new operation is never allocated on retry. |
| T075 compensation | Barrier after staged effect, before owner CAS/cleanup. A second actor updates resource pointer, file incarnation, secret key reference or package version. Old compensation refuses to erase it. A known postcommit warning never restores retryable uncommitted state. |
| T077 authority/lifetime | Revoke grant, disconnect OAuth, change storage endpoint, switch database, delete/recreate owner or expire execution claim while dispatch is blocked. No new dispatch after rejection; already dispatched result is retained, cannot revive a disconnected generation, and cannot publish/clean a different target. |
| T078 restart/retention | Replay exact effect after later user edit, receipt compaction, closed epoch and dataset restore/import. Exact retained result is separate from current object; expired evidence refuses unsafe resend. Restart does not replay old plugin stop into a new runtime incarnation. |
| T079 staged batches | Fail item N after acceptance while N+1 is unsent. Record per-item identities/results, retain known successes, and resume only safely resolvable items. No whole-batch atomic-success claim or full blind replay. |
| Secret/OAuth privacy | Fake vault and token endpoint use synthetic values; capture logs/status/pending serialization and assert no raw credentials, token response or weak secret fingerprint. Lost vault/token acknowledgement, concurrent refresh, callback/disconnect and migration cleanup have explicit stage outcomes. |
| Remote set/identity races | Fake Graph handler returns categories then concurrent edit before PATCH; conditional mismatch must preserve the other set or protected operation is refused. Gmail replay after user reversal is not silently reissued. Docker same-name/different-ID or config is not accepted/removed. |
| Native UI/API/tool proof | Shared renderer keeps dirty values and operation identity after conflict/unknown; explicit current read does not retokenize submitted bytes. Host stores exact acceptance before rendering. Two browser contexts exercise the real owner; tool/workflow adapters cannot omit required expectation or reinterpret Uncertain as retryable Failure. |
| T076 only if delayed projection selected | Duplicate/reorder/poison delivery cannot replace a newer accepted projection; delete tombstone wins and unrelated owners progress. Do not add a meaningless outbox test when the projection remains in the owner transaction. |

Use barrier/fault-injection seams and exact output assertions, not timing sleeps.
Reuse `tests/Support/CanDoItAll.DurableFileWriter.TestHost` for cross-process
filesystem proof and an existing suitable isolated PostgreSQL fixture for owner
CAS/receipt races. HTTP/FTP/IPFS/native-vault/process fakes prove adapter behavior;
they do not prove real provider guarantees. A real-provider qualification lane,
if required, must use a dedicated disposable service/account and explicit owned
resources. Never use a user's mailbox, vault, shared container or ordinary5032.

Performance scenarios refine architect PERF-07/09/10: one target versus many
disjoint targets, bounded max-size content, restart reconciliation backlog,
staged batch failure, and slow provider while unrelated SQL owners save. Existing
stable placement/FTP/IPFS limits are 256 MiB and stable reconcile uses bounded
readback; the interactive file limit is `FileAccessHandleOptions.MaximumContentBytes`.
Use those configured bounds, not new guessed payload limits. Measure peak memory
from frozen input plus hash/upload/readback buffers, p50/p95/p99, throughput,
provider request count, lock wait and journal growth. Existing stable placement
copies bytes; avoid adding one more whole-content copy per orchestration layer.

Acceptance proposals to ratify against the baseline: zero provider calls inside
an open retriable owner transaction; zero unsafe redispatches in injected unknown
cases; zero stale cleanup successes; no global lock/queue shared by disjoint
targets; bounded per-owner recovery work and independently progressing unrelated
operations. Numeric latency/throughput thresholds and backlog limits require the
isolated measurements before their slice closes. Do not convert these proposals
into fabricated performance results.

## Cleanup and closure evidence

Record ownership before creating test resources: exact temporary root, database
lease, child process, container/volume/network and synthetic provider objects.
Use repository temporary-directory helpers and existing isolated fixtures.
Dispose streams/contexts/child processes before deleting the exact owned root.
On Windows resolve and verify containment before recursive deletion. Keep crash
artifacts only until their recovery assertion/proof is retained. Do not globally
prune Docker, remove `.artifacts`, or alter ordinary5032, installed tools or user
vaults. A deliberately uncertain fixture effect must be terminated/reconciled in
its fake/dedicated provider before cleanup, not abandoned after a timed-out test.

Each slice closes with exact source/contract version, discovered/passed/skipped
counts, driver modes actually proven, unknown/unsupported modes, migration and
restart evidence, performance results, and cleanup ledger. Parent serializes
standard Release/Debug outputs and records portability-static plus rendering
register enforcement when protected source changes. Existing P02a PostgreSQL
passes are unrelated to these not-yet-executed external-effect proofs.
