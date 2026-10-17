# P03 stopped-host release reconciliation and legacy installation

Source-only implementation proposal extending the frozen [owner-completion decision](P03-durable-owner-completion-refresh.md), SHA `56F7C429B40D94047427640AC95891AADF1104C8999D15DEA7A0064BB0FAB487`. No production/schema changes or execution. Stage-2 runtime and separate P03 implementation review remain gates; Resource stage 3 remains held.

## Operational decision

Use one explicit offline command for one exact completion or legacy subject. Reuse lifecycle exclusion, existing Projects keys, participant records and canonical workspace coordination. No new lease service, background reconciler, startup repair or online override.

The operator must terminate **all** hosts able to use this database or original source workspaces: workers, old binaries, transfer/CLI processes and suspended processes. Disable restart mechanisms; stop profile/path configuration and activation maintenance. Maintain this condition through command completion and terminal cleanup, **including the probe-release → Released-CAS interval**. On command crash/uncertain exit, keep hosts stopped and repeat the same command after confirming its old process terminated. Paused/disconnected is not stopped.

`--writers-stopped` is an operator attestation, not machine proof of global termination. SQL/file acquisition verifies instantaneous exclusion from cooperating owners; it cannot prove that an old owner will not resume or another host start. PID absence, claim expiry, current authorization and elapsed time never clear a gate. An environment unable to establish the stopped/no-restart precondition remains held. Online recovery needs a separately proved admission fence.

Exact source evidence:

- `Web/Infrastructure/DatasetActivationCommand.cs` and early `Web/Program.cs` dispatch provide the explicit-profile offline pattern. Do not inherit automatic Migrate/bootstrap.
- `DatasetLifecycleStore` supplies shared mutation/exclusive maintenance exclusion. Warm `DatasetActivationService` cache reads bypass the activation-file lock; that lock alone cannot stop writers.
- `CanonicalAgentCatalogLeaseSource` uses organization scope for the profile. `FileSandboxWorkspaceStorageLayout.WorkspaceLockPath` and `FileSandboxWorkspaceCrossProcessLock` identify its existing `workspace.lock`. `FileSandboxWorkspaceStore.CatalogReadLease.cs::DisposeAsync` provides no durable release witness.
- `WorkspacePathResolver.ResolveWorkspaceRootForRead` selects profile/configuration/default location. The database fingerprint excludes this path; mutable `HostBoundPathRecord` must be copied, not retained by alias.
- Process source disposal delegates to its held project authority. `EfProcessExecutionMutationGuard.RequireDispatchAsync` adds transaction-owned Process locks to enlisted SQL, not another persistent file lease. Any new source domain needs explicit recovery support.

## Typed command and source capture

Add `ProjectDeletionRecoveryCommand`, early-dispatched before normal host startup, with closed actions `Inspect`, `Reconcile`, `InspectLegacy`, `InstallLegacy`. Every action names explicit profile/expected namespace/completion ID; legacy additionally names exact project/lifetime IDs. Supply one completion ID and retain it on retries. Mutating actions also require the stopped-host acknowledgement and typed SHA-256 plan digest from inspection. Reject repeated/unknown options, empty/non-D-format GUIDs and malformed digests. No raw path, SQL, participant override, replacement namespace or force option.

Inspection is informational and bounded. Its versioned plan digest covers exact subject/binding, compiled participant protocol, stored version/state and record digests; exclude inspection time, new randomness and display text. Recompute before mutation. The digest is not release authority.

Use `(DatasetNamespaceId, ProjectDeletionCompletionId)` and outcomes `Observed`, `InstalledAwaitingRelease`, `Released`, `Unconfirmed`, `Refused`. Unconfirmed identifies the attempted install/mark stage and never means the deletion did not commit. Known installation survives later failure as InstalledAwaitingRelease. Typed reasons include binding/plan change, exclusion busy, unsupported legacy evidence, missing completion, participant mismatch and read unavailable. Logs retain exceptions/safe IDs, not credentials, payloads or physical paths.

Construct only explicit-profile PostgreSQL stores using `CoordinatedDatabaseTransaction.ForProfile(profile)`. No host Start, database creation, Migrate, activation attach/reactivate, provider preparation or execution-workspace factory. Schema/protocol mismatch refuses. A private invocation-local observation is created only after every physical probe successfully reaches terminal release; it binds the exact plan/completion/binding and is consumed in the same coordinator. No public boolean, serialized release certificate, generic callback or reusable cross-process proof.

New completion metadata must capture the **actual acquired** source adapter's immutable release description: NoExternalSourceLease or the supported canonical organization-workspace binding, including original normalized root/host/path policy/scope. Paths remain private; external output contains only their identity digest. Freeze the complete original project-admission set needed for SQL exclusion. Reconstruct owner-defined operation/hierarchy/participant keys from typed origin and installed protocol, never client lock strings. Unknown domains, incomplete keys or changed path binding refuse; current location cannot substitute for the captured original. Every source implementation must handle capture explicitly; no unknown-to-None default.

A narrow concrete Persistence helper validates an existing root/scope and traversal, then acquires the same existing layout/cross-process lock. No catalog load, Ensure, split recovery, execution read or provider work. Missing/rebound source refuses, without creating a replacement workspace. Opening/creating the existing protocol's coordination file is its only file effect. Supported filesystem semantics remain those of the existing primitive, not a new distributed-filesystem guarantee.

## Reconcile one existing completion

1. Read the exact completion and matching Active file/SQL binding, installed protocol, immutable participant set and native committed-deletion evidence; compare plan digest. Missing/foreign/imported/unsupported evidence refuses. Observe an exact already-Released gate without repeating deletion. Finite receipt expiry does not invalidate the separate gate/native retirement.
2. Acquire existing activation-file coordination, reread Active binding, then captured source exclusion, **before SQL**. Use one five-second aggregate acquisition budget linked to cancellation. Synchronous filesystem calls remain cooperative, not a hard OS deadline.
3. Fresh ReadCommitted explicit-profile transaction: lifecycle first, `LOCK TABLE "Infrastructure_DatasetLifecycle" IN EXCLUSIVE MODE NOWAIT`, then exact Active row `FOR UPDATE NOWAIT`. Acquire complete distinct ordinal keys with nonwaiting transaction advisory locks. No session handoff or waiting edge under held source/lifecycle/keys. Stage-1 import NOWAIT remains prerequisite. Recheck exact completion, retirement and keyed participant evidence under real enrollment; no database-wide receipt scan/payload count. Use a named Infrastructure offline entry; retain generic waiting semantics.
4. Roll back the read-only probe; terminally dispose transaction/context, then source and activation leases. Retain primary plus cleanup failures. Any uncertain acquisition/release acknowledgement leaves Awaiting unchanged and skips mark. Apply existing safe pool quarantine; no waiter-only timeout, concurrent retry or detached cleanup. The external owned-process deadline is separate from cooperative disposal.
5. Only after every probe released successfully, re-resolve/compare frozen profile/source identity and observe the same local Active record. In a fresh short Projects transaction, lifecycle-check that binding and CAS exact unchanged address/version/origin/participant set Awaiting→Released. Hold no source lease, P/B/hierarchy exclusion, provider or callback. **Operator no-restart covers the gap**; earlier probes do not. The lifecycle predicate separately fences namespace change.
6. Preserve commit-attempted/acknowledged knowledge outside cleanup. Lost mark acknowledgement requires terminal unwind then exact gate observation: exact Released establishes success; missing/unavailable/conflicting evidence remains Unconfirmed. Cleanup after acknowledged mark cannot downgrade Released because original/probe release was already established. No participant runs during the command, even on success.

Releasing probes before the mark prevents the administrative probe's own uncertain disposal recreating the original defect. The final mark adds no file ownership; visibility of its committed row implies that transaction's locks released. Neither fact proves global host termination or replaces current authority for later cleanup.

## Legacy installation without invented historical authority

Migration adds nullable completion-binding fields to participant records without rewriting payloads, IDs, statuses, claims or timestamps. Ordinary readiness refuses executable unfinished project-deletion work without valid binding; completed notices remain readable. Offline command dispatch is available before runtime readiness. Old binaries remain stopped and must fail the reviewed Projects protocol-version cutover.

Inspect one exact profile/project/lifetime through owner stores. Require native non-imported retirement, no live project under that ID, no residual state the existing cleanup rejects, and exact bound records. Workbench must be DeleteProject with valid matching SourceReference; Agent must have the matching nonempty stored profile/lifetime pair. Include a completed partner when another remains unfinished. Never infer lifetime from current project/latest retirement, include node cleanup or treat imported history as local authority.

Initial administrative refusal limits: at most 16 candidate deletion records for the subject across installed owners (LIMIT 17 header preflight), at most 2 MiB raw JSON per Workbench record checked before text materialization, at most 64 KiB complete encoded plan. Inspect sequentially, retain typed identities/status/digests rather than all decoded payloads. These are proposed compatibility boundaries, not established retained-data maxima or allocation guarantees. Oversized, unbound, malformed, foreign, ambiguous, imported or partial evidence blocks installation/activation without changing bytes. Supporting such a retained case requires an explicit owner-specific repair; no truncation/adoption/discard fallback.

Legacy records lack original operation namespace and source-path capture. Use origin LegacyInstallation and its **installation namespace**, not a fabricated historical mutation namespace. Mint no Accepted mutation receipt, revision, deletion timestamp or user operation. The operator attests termination of all former source-owner hosts, including former workspace locations; the command probes the current canonical organization workspace/database. It cannot reconstruct or machine-verify lost historical paths. Inability to make that attestation remains unsupported/held.

Install repeats inspection under the same exclusions. There is no historical operation/source-key reconstruction for legacy; use compiled target/hierarchy/participant keys and state that limit. Lock each bounded participant record before checking exact payload digest/identity. Atomically insert one Awaiting completion with retained installation-plan digest and bind exactly those recovery IDs through their enlisted owners. Unique subject/origin and recovery-binding constraints prevent alternate IDs. Exact already-installed state is observed; partial/different binding refuses. Preserve claims/counters/errors/storage outcomes. Share exact validation with live participants, not parallel Web SQL.

After install commit, perform terminal probe release and fresh mark. Crash leaves a resumable hold: inspect/reconcile the same completion. Lost installation acknowledgement needs exact gate, all bindings and retained plan-digest observation; never generate a new ID. Claim expiry permits no callback until Released plus current authority. Enforce at Workbench dependency/direct-processor entry, Agent claim/revoke, Projects retry and initial postcommit loop. Missing gate never grants legacy permission.

## Lifecycle and retention

Receipt expiry cannot release/delete a gate; missing gate/participant evidence never means terminal work or noncommit. Initially retain all gates, including Released, without automatic purge: the smallest safe lifecycle, with explicit storage-growth cost. Keep the proposed 16 KiB encoded gate/reference cap, exact keyed lookups and indexed namespace/release-state inspection. Activation needs accumulated-terminal-gate/maximum-plan capacity evidence and bounded count/oldest-pending diagnostics. This is not bounded retention or a new worker.

A later purge needs separate release: exact immutable terminal evidence from every participant, closed finite horizon, no supported retry/compensation reopening, fail-closed missing lookup and no cross-owner FK/cascade/lock inversion. Claim timeout or absence never qualifies.

Before Reactivate/Resume closes an active namespace, Composition must run enlisted owner checks under lifecycle exclusion: Awaiting gates **or any unfinished bound obligation** block closure. Released-but-unfinished work also blocks because dispatch needs the original active namespace. Indexed EXISTS checks contain no payloads; missing/unknown ownership refuses. Preflight before Prepared publication improves diagnostics, but repeat before SQL closure. Retain an already-published Prepared plan on refusal and existing explicit Resume/repair rules. Terminal gates may remain inert in old namespaces. Matching ordinary readiness may serve reads with valid Awaiting gates while callbacks remain blocked; missing/inconsistent binding still refuses. Transfer target residue includes gates; business export/import never carries release permission.

## Exact prospective edits

| Owner/files | Minimal responsibility |
| --- | --- |
| Existing proposed Projects.Contracts lifecycle types; Projects ProjectDeletionCompletion.cs | Typed address/plan/outcomes/origin/source binding; exact inspect/install/CAS and transition-refusal queries. No public caller assertion of release. |
| Projects ProjectMutationAuthority.cs; existing Agent/Process source adapters/canonical catalog source | Capture actual held-source identity and complete original admissions before commit; explicit updates to every implementation. |
| New Web Infrastructure/ProjectDeletionRecoveryCommand.cs; Program.cs | Early offline parser/dispatch, no startup or migration. |
| New Composition/ProjectDeletionRecoveryCoordinator.cs | Explicit-profile orchestration and terminal ownership. No generic strategy/registry. |
| New Persistence Storage/FileSandboxWorkspaceMaintenanceExclusion.cs; Infrastructure ControlPlane/DatasetLifecycleStore.cs | Existing file-lock probe and named nonwaiting offline lifecycle entry. |
| New Workbench CrossModule/ProjectWorkbenchDeletionRecoveryStore.cs; AgentFramework Persistence/AgentProjectDeletionRecoveryStore.cs | Data-only exact lookup/legacy inspection/bind/unfinished checks shared with participants; owner options/coordinated transaction only. Composition names both concrete stores with the same compiled IDs, no second configurable registry. |
| Existing Workbench deletion participant/processor, Agent deletion participant, Projects retry | Exact gate before dependencies/claim/dispatch; retain existing claims/execution. |
| Composition activation/bootstrap; transfer inspection; parent canonical migration/owner mappings | Protocol cutover, completion indexes and nullable participant binding fields; missing-binding/transition refusal. No payload rewrite, cross-owner FK/cascade or epoch promise change. |

Data-only stores are justified by actual constructors: Agent mutation participant requires IAgentFrameworkWorkspaceService; Workbench requires storage planning/processors. Offline inspection must avoid constructing that runtime graph. Move/share existing exact record validation, not authority or execution.

## Decisive executable proof

No case counts before authoring. Reuse isolated PostgreSQL/TestApplication and real scoped workspace; child processes require existing ownership/cleanup records.

- Real child holds catalog/lifecycle/P/B: bounded refusal and unchanged rows; after terminal release, same command succeeds with zero provider/runtime/participant dispatch. Wrong binding/path, missing source and unknown domain refuse.
- Fault every acquisition/release and release-to-CAS boundary: hold survives release uncertainty, no concurrent cleanup. Test observable namespace/profile changes without claiming machine enforcement of no-restart.
- Crash after install; lost install/mark acknowledgement; repeated/two-actor command: one immutable binding and exact knowledge, no new deletion/operation/epoch/callback.
- Native/compensation legacy, completed-plus-pending, unbound/foreign/imported/recreated/partial/malformed/oversized cases: old rows unchanged except new bindings; pre-materialization count/byte and final encoded limits.
- Fresh HTTP/native/restart/direct-processor actors remain blocked while Awaiting, even with empty lists/expired claims. Released still needs exact lifetime/current authority and claim admission.
- Receipt expiry, old retained gates, namespace transition/transfer before and after terminal work, accumulated-gate and maximum-plan costs. Measure reads/lock wait/hold/allocations; bounded sequential work is not constant latency.

This is source-derived design. Physical exclusion, crash/release proof, schema/legacy compatibility, capacity, complete caller/producer cutover, parent builds/static enforcement and operational no-restart runbook review remain unexecuted release requirements.
