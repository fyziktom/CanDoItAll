# Processes native authoring authority prerequisite

Status: **required separate implementation scope; native authoring durability is blocked**.

This record follows PC2 on `components-decoupling`, product checkpoint
`8951ffdf7d35aaf0b00b7fe8220f1aad8d9e3d59`. It does not authorize or implement a new
schema. Rendering remains in `CanDoItAll.Processes.UI`; native reads, commands,
project admission and launch remain with their current owners. See
[UI seams](ui-component-seams.md) and [the Processes boundary](processes-ui-boundary.md).

## Executable diagnosis

The isolated PC2 probe resolves `IProcessWorkspaceProjectionClient` through
`TestApplicationBootstrap.BuildServiceProviderAsync`, backed by a task-owned
PostgreSQL 18.6 database. It edits a real distributed template-derived definition,
then observes the same client, another client/service scope and a fresh native DI
root against the same database. The fresh root is an in-process host restart,
not a claim that an operating-system process was restarted. The implementation's
instance dictionaries also cannot survive an operating-system restart.

The private, retained reproducer is
`.artifacts/pc2-20261009/diagnosis/Program.cs`, built with
`dotnet build .artifacts/pc2-20261009/diagnosis/diagnosis.csproj -c ProcUiProofPC2 /m:1`.
Run the resulting `diagnosis.dll` with a report destination and the explicit owned
`CANDOITALL_TESTS_POSTGRES_CONNECTION` environment variable. Do not print that
variable or use the development database. Reports and logs are under the same
owned run directory; [the validation record](../validation/processes-ui-pc2.md)
records their hashes and final results. This diagnostic is outside normal Stable
tests: data loss is an observed defect, never an expected passing product contract.

| Native command | Actual receipt/result | Following client observation |
| --- | --- | --- |
| Definition SaveDraft | Accepted; submitted unique name and a new opaque revision | Original name/version returns even on the same client, because its next call creates another scope |
| Definition Publish | Accepted when submitted against the original template revision | Catalog remains template-derived; new launch preparation uses the template definition |
| Role SaveRole | Accepted; unique purpose returned | Original purpose returns |
| Step SaveStep | Accepted; unique title returned | Original title returns |
| Template ImportProcess | Accepted; imported-component count becomes one | Imported-component count returns to zero |
| Canvas MoveNodes | Accepted; requested coordinates returned | Same client retains positions; another client and a fresh root return template geometry |
| Subsequent Save with accepted revision | Rejected | The new command scope recognizes the template revision, not the previous accepted revision |

All four authoring content changes disappear in the fresh root. The catalog still
reports 27 published definitions and zero drafts in the tested default pack.
Canvas retention is circuit/session state; it is not evidence of durable authoring.
No production provider call or actual process execution is needed for this diagnosis.
The extended probe also uses the native `IProcessLaunchOperatorAuthoritySource`
and a fresh caller intent to persist a real preparation. Its immutable initial
plan and review survive the fresh root with the unchanged template name and
definition version. A task-owned project Save likewise returns Accepted but loses
the name across scopes and the fresh root, even though the captured profile,
project ID and project lifetime binding remain identical. This distinguishes the
authoring defect from an intentional authority change.
The native launch/workflow/file/chat browser lane separately proves existing runtime
effects. Do not equate those durable runtime records with durable authoring.

## Current owner and consumer map

Paths below are relative to the repository. These are current symbols, not proposed
new implementations. CodeAnalytics was absent from actual tool discovery in PC2;
the map was checked from source, call sites, DI, evaluated project graphs and tests.

| Concern | Current source and behavior |
| --- | --- |
| Native adapter | `src/Modules/CanDoItAll.Modules.Processes/Services/ProcessWorkspaceProjectionClient.cs`: `GetShellAsync` and definition/role/step/import commands open new scopes; only `canvasSessionService` is retained in the calling scope |
| Composition | `Services/ProcessesModuleServiceCollectionExtensions.cs` in the same module registers the five editor/catalog services scoped |
| Definition state | `src/Processes/CanDoItAll.Processes.Application/ProcessDefinitionEditorProjectionService.cs`: `snapshots`, `ExecuteSaveDraft`, `ExecutePublish`, archive/delete/clone receipts and identity/governance/contracts/simulation projections are instance state |
| Role state | `ProcessDefinitionRoleEditorProjectionService.cs` in Application: `snapshots`, `ExecuteAddRole`, `ExecuteDeleteRole`, template overrides and workflow preferences are instance state; returned selection is authoritative for that result |
| Step state | `ProcessDefinitionStepEditorProjectionService.cs` in Application: `snapshots`, `StoreDraft`, `NormalizeDraft`, branch/artifact normalization and decision-role bindings are instance state |
| Import state | `ProcessTemplateCatalogProjectionService.cs` in Application: catalog/template loading and imported-component snapshots do not commit a canonical definition aggregate |
| Canvas | `ProcessDefinitionCanvasEditorProjectionService.cs` in Application: snapshots keyed by scope/definition and captured project binding; geometry, selection and structural commands share a session owner, without a durable definition store |
| Catalog/defaults | `ProcessDefinitionCatalogProjectionService.cs`, `ProcessTemplatePackLoader.cs` in Templates, and `templates/Processes/manifest.json`: distributed defaults determine search, counts and stable catalog keys |
| Identity | `ProcessTemplateKernelBuilder.Build` and `CreateDefinitionId`: deterministic definition ID from template key, content-derived definition-version ID, stable step/artifact IDs from keys; editor receipt/version tokens are separate from these launch identities |
| Read API | `src/App/CanDoItAll.Web/Api/ProcessDefinitionsApi.cs`: catalog, definition, roles and steps are read endpoints using the same projection services. They expose no definition write API |
| Launch selection | `ProcessLaunchApplicationService.ResolveDefinition` loads the pack/definition, with optional live-run profile. `PrepareLaunchAsync` invokes `ProcessTemplateKernelBuilder.Build`, `ProcessInstancePlanCompiler.Compile`, driver catalog and executor resolution |
| Executor resolution | `Services/RuntimeIntegration/AgentFrameworkProcessLaunchExecutorResolver.cs` in the native module consumes the selected definition's steps/roles, provider/agent catalog, workflow catalog, workflow availability and host capabilities. A UI role snapshot does not replace this definition |
| Prepared/accepted plan | `ProcessLaunchApplicationService.Prepared.cs`: `PrepareDurableLaunchAsync` saves the compiled initial plan, assignments and review. `EfProcessPreparedLaunchStore`, `ProcessPreparedLaunchCodec`, `EfProcessRuntimeUnitOfWork` and instance-plan persistence preserve accepted launch identity and immutable runtime inputs |
| Existing database | `src/Processes/CanDoItAll.Processes.Persistence/ProcessPersistenceDbContext.cs`: prepared launches, instance plans, runtime state/steps/assignments, artifacts, idempotency, outbox, projection/history and run records. It contains no canonical editable definition aggregate |
| Project/profile authority | `src/Modules/CanDoItAll.Modules.Projects/ProjectLifetime.cs` and `ProjectWriteAdmissionMutationGate.cs`: `ProjectWriteAdmissionService` captures profile/project/lifetime and checks current admission within coordinated mutations. `ProjectProcessLaunchAuthorityService` in Workbench captures and revalidates native launch authority |
| Transfer/retirement | `DatabaseTransfer/ProcessesProjectTransferTargetStateParticipant.cs` in the native Processes module currently accounts for runtime/prepared-launch residue. An authoring store would need an explicit transfer/deletion policy and participant coverage |
| UI lifetime | `ProcessWorkspaceShell.razor` owns operation/read fences, captured profile generation, project binding and opening state. The UI preserves accepted observations across stale reads; that cannot make them persistent |

The existing prepared-launch/instance-plan store is a real durable owner, but its
aggregate is an accepted execution preparation. Overwriting it to implement an
editable definition would change runtime authority and historical evidence. The
projection snapshot/history store is likewise not an editable definition authority.
No established durable authoring owner was found for a bounded adapter repair.

## Proposed smallest native authority

Introduce one native **definition authoring aggregate** in a separately approved
slice, with contracts in Processes.Contracts, behavior in Processes.Application
and PostgreSQL persistence in Processes.Persistence. Proposed names in this section
are design choices, not existing types or tables. Do not add dependencies to UI.

The aggregate identity must include database profile and an explicit global scope
or captured project lifetime, plus the stable definition identity/key. Store one
versioned draft document containing identity/governance/contracts/simulation fields,
roles with workflow preferences, steps with decision-role/operation/contract/route/
artifact/subprocess bindings, imported provenance and structural canvas data.
Publish produces an immutable definition revision. Keep view-only selection,
zoom/pan, floating-window state and unsaved input buffers outside this aggregate.
Persist authored node positions only as explicitly declared authoring layout;
never interpret a camera/view change as a structural definition mutation.

Use an explicit schema version and typed serializer at the persistence boundary.
Reuse established definition/step/role/artifact key and launch-ID conventions.
Preserve template-derived definition IDs; derive published version IDs from
canonical committed content using the existing kernel builder convention. Assign
new stable identities only to deliberate copies/new definitions. Reject duplicate
keys and dangling decision roles, routes, artifacts, workflow or subprocess mappings.
Deleting a referenced role must either produce a specific validation rejection or
an explicit reviewed repair command; do not silently reassign decision authority.

Use a database row revision with atomic compare-and-swap against the exact expected
revision. All five authoring families observe the same aggregate revision. Rejected
commands leave state/revision unchanged. Persist a typed command identity, request
fingerprint and result receipt in the same transaction so transport interruption can
be reconciled without replaying Add/Import. Repeated identity with changed payload
is a conflict. Return the committed projection and revision only after commit.

Capture trusted profile/project lifetime at admission and revalidate it inside the
coordinated write transaction using existing admission infrastructure. Never locate
a successor lifetime by public project ID after capture. Global overlays belong to
the active database profile; they must not leak through singleton dictionaries.
Project reads and writes require their existing authorization boundaries, including
agent/API callers when those callers gain authoring access in a later scope.

## Catalog, publication and compatibility

Distributed template packs remain immutable. Read catalog entries from a native
overlay over the current pack, with explicit provenance/base-content hash and
deterministic precedence: project overlay for the captured project lifetime, then
global published overlay, then distributed default. A draft is visible through
authoring but does not silently become a runnable published version. Explicit
archive/tombstone hides an overlay according to its documented scope; deleting a
custom definition and resetting to a distributed default are different commands.

Index catalog identity, name, scope, publication state and revision metadata;
avoid deserializing every full document for search/counts. Import copies content
and provenance atomically into the target aggregate with deterministic conflict
handling. Export records schema version, stable keys and provenance, excluding
credentials and executable authority grants. Pack updates never overwrite a local
draft: compare base hashes and require explicit reconciliation.

Replace launch template selection with one native resolution boundary that returns
the exact published immutable definition content and provenance. Feed the same
selected content to kernel compilation, subprocess resolution, executor resolution,
readiness, prepared review and persisted initial plan. A preparation pins that
revision; retries use it even if a new revision is later published. Already accepted
runs and stored plans remain untouched. A prepared caller must not silently switch
to a newer publication during acceptance. Preserve existing caller-intent,
profile/project admission, atomic commit and continuation contracts.

Existing read API routes and lightweight projections remain compatible. Adapt their
current native services to the canonical aggregate; no new HTTP transport is needed
for the Blazor UI. Add API writes only under separately specified authorization and
idempotency contracts. Existing installations without overlays keep identical
template-derived identities, published versions and launch behavior.

## Implementation and proof sequence

1. Freeze aggregate/serialization/version/retirement contracts. Add native owner
   contract tests for all editor families, complete hidden semantic fields and
   cross-family referential integrity. Keep the UI project graph unchanged.
2. Add an additive PostgreSQL migration for draft aggregates, immutable published
   revisions and command receipts, scoped by profile/project lifetime. Rehearse
   upgrade from an existing template-only database, backup and restore. No automatic
   conversion of request-local dictionaries is possible because their data is not
   durable; do not claim otherwise.
3. Implement transactional owner and native adapters. Test competing clients, stale
   expected revisions, repeated identical/conflicting intent, errors before/after
   commit, real new service scopes, a separate process restart, profile changes,
   project deletion/recreation and disposal. Portability-static is mandatory.
4. Adapt catalog/read API/template import/export and declare project transfer,
   deletion, retention and restoration participation. Test global/project precedence,
   no cross-profile leakage, duplicate imports, defaults and pack updates.
5. Adapt launch resolution and all compiler/executor/subprocess consumers together.
   Prove a newly published change reaches an actual prepared and accepted instance
   plan, while old plans/retries retain their exact version. Run the existing atomic
   launch, producer/API, Workflow-versus-Agent, project-source, native workflow,
   files/chat/cancellation and voice lanes alongside new owner tests.
6. Replace the diagnostic's loss observations with positive end-to-end native
   component/browser/API oracles across scopes and a process restart. Run affected
   source/published sandbox modes, evaluated closure, documentation/secret checks and
   a named broad gate at the final frozen native-owner checkpoint.

Rollback requires a retained backup and a version-compatible reader. A deployment
must refuse to launch an overlay revision it cannot decode; silently reverting to
distributed defaults would execute different work. Preserve additive data during
application rollback, and never delete accepted plans to make downgrade pass.

## Alternatives rejected

Extending dictionary lifetimes to singleton would add process-wide data leakage and
still lose data at restart. Browser storage or a light UI store cannot enforce
database/profile/project authority. Editing distributed template files would mix
deployment assets and user data. Independent per-panel tables would reproduce the
split revision and referential-integrity problem. Reusing runtime plans as editable
drafts would corrupt immutable execution history. A native persistent overlay with
one commit boundary is the smallest option that meets all these constraints.

The unresolved work is this native owner slice. PC2 UI corrections, structural
decoupling and carried-forward runtime effects have separate validation statuses;
none of them constitute durable Save/Publish parity.
