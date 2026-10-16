# C# current-state responsibility inventory

Inspected 2026-10-16. Read-only investigation preceded implementation; no builds
or tests were run for this inventory. Source observations below are not reproduced
runtime test results. The architect archive is preserved under `../inputs/architect-v0.1`
as a historical reference and does not itself authorize commands or change current
repository rules.

## Tool and graph evidence

CodeAnalytics snapshot `snap-20261016142513-0f60f349` scoped the current product
solution to `CanDoItAll.Infrastructure`, `CanDoItAll.Modules.Projects`, and
`CanDoItAll.Modules.Projects.Contracts`. It loaded three projects and 205 documents;
the scoped dependency query reported no project cycles. Scope is deliberately
incomplete: absence of other references in that snapshot is not evidence that
they do not exist. Actual `.csproj` files were read to verify direct references.
Parent bundle investigation also covers a broader foundation snapshot.

| Project | Actual direct project-reference direction relevant to this work |
| --- | --- |
| Projects.Contracts | SharedKernel only |
| Infrastructure | Infrastructure.Abstractions, SharedKernel |
| Projects | Projects.Contracts, SharedKernel, Infrastructure, AgentFramework.Core/Models, Projects.UI/Files.UI, AppComponents/Navigation, FileTools.Integration.Abstractions |

Keep the current Contracts/SharedKernel inward direction. Do not add Projects
references to Workbench or Process implementation. The existing Projects-owned
source-authority port and outward Workbench adapters are the appropriate seam.
This slice does not justify moving all Projects implementation into a new generic
Core or coupling all owner DbContexts through one mutable object repository.

## Exact current owners and evidence

| Owner / file | Current responsibility and observation | Required treatment |
| --- | --- | --- |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs` | Root/phase/option/hierarchy mappings; large partial ProjectsService; definition reads and writes; hierarchy; deletion; search/activity | Isolate definition handling only, leaving thin compatible delegation; do not add another permanent responsibility partial |
| `ProjectModels.cs:14` | Project has LifetimeId but no content token or IHasConcurrencyToken | Add aggregate definition stamp; preserve lifetime meaning |
| `ProjectModels.cs:489` | GetAsync reads root/phases/options separately and returns blank create draft for unknown existing ID | Add coherent bounded protected edit read and explicit missing outcome |
| `ProjectModels.cs:569` | SaveEditorAsync validates original admission/profile but calls common whole replacement | Carry content expectation through same UI path; original lifecycle alone is insufficient |
| `ProjectModels.cs:693` | SaveWithReceiptCoreAsync takes current DB entity; optional lifetime check; complete child replacement | Require original edit expectation, root CAS, atomic child/receipt/projection write |
| `ProjectModels.cs:831` and `:862` | Missing children deleted; repeated list scans; unknown IDs create new rows | Linear identity maps; reject duplicate/foreign/unknown IDs; explicit new IDs |
| `ProjectModels.cs:864` | Blank submitted existing option skipped after retained-row deletion decision | Specify/test clearing an existing option; do not retain hidden old values |
| `ProjectModels.cs:911` | Commit then explicit dispose then old-payload search callback | Preserve commit knowledge; move small search participant into same transaction or implement full durable alternative |
| `ProjectModels.cs:967` | Search callback re-acquires project gate and validates lifetime only | Content ordering is not protected; remove the stale callback path |
| `src/Modules/CanDoItAll.Modules.Projects/Persistence/ProjectsDbContext.cs` | Six owner mappings; generic application-managed token hook already present | Add exact new mappings; no schema initializer |
| `src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs` | Canonical complete model and token hook | Continue sole canonical migration authority |
| `src/Foundation/CanDoItAll.Infrastructure/Persistence/ApplicationManagedConcurrencyTokens.cs` | Stamps added/modified IHasConcurrencyToken entries | Does not stamp a root merely because a child changed; root participation must be explicit |
| `src/Modules/CanDoItAll.Modules.Projects.Contracts/ProjectPortfolioModels.cs` | Data-only editor models; optional ExpectedLifetimeId; full replacement lists | Add typed definition expectation; missing update expectation must reject across every adapter |
| `src/Modules/CanDoItAll.Modules.Projects.Contracts/ProjectEditorAcknowledgement.cs` | Accepted normalized values and submitted-index/child-ID mapping | Extend with accepted stamp/operation identity; preserve post-dispatch draft reconciliation |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectLifetime.cs` | Profile/lifetime capture and enlisted batched checks; PostgreSQL FOR KEY SHARE | Preserve lifecycle guard; do not claim it protects ordinary content changes |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectMutationAuthority.cs` | Source-first authority; distinct authorized PostgreSQL branch; exact lifecycle-set admission | Preserve order and explicit source lease; expose commit knowledge across cleanup |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectCreationReceipt.cs` | In-memory creation fingerprint/compensation evidence | Not durable ordinary tool replay; retain and link to owner result |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectCreationReservations.cs` | Durable creation reservation/lifetime lifecycle | Reuse for create admission; no invented duplicate creation authority |
| `src/Modules/CanDoItAll.Modules.Projects/DatabaseTransfer/ProjectsProfileTransferStore.cs` | Explicit locked maintenance import/export; fresh target lifetime; imported history | Fresh local stamp; preserve/inert receipt provenance; update replacement preflight |
| `src/Foundation/CanDoItAll.Infrastructure/Persistence/SerializableMutationScope.cs` | 64 process stripes, Serializable transaction, sorted per-key advisory SQL | Fix finally cleanup; measure false contention and per-key round trips before changing strategy |
| `src/Foundation/CanDoItAll.Infrastructure/Persistence/CoordinatedDatabaseTransaction.cs` | Actual shared connection/transaction; profile checks; retired participation guard | Reuse; no parallel commands or detached work inside active participation |
| `src/Foundation/CanDoItAll.Infrastructure/Search/SearchIndexing.cs` | Search-owned UpsertForMutationAsync participant; unique SourceType/SourceKey | Enlist the small project projection; no direct Projects access to Search rows |
| `src/Foundation/CanDoItAll.Infrastructure/ControlPlane/CanonicalRuntimeDatabase.cs` | Canonical profile; Generation is zero | Not a durable restore epoch; control-plane activation needs explicit proof |

## Enabled mutation perimeter

| Entry point / path | Current route | Conversion requirement |
| --- | --- | --- |
| Projects Blazor host, `src/Modules/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor` | SaveEditorAsync / CreateEditorAsync with original lifecycle and existing acknowledgement reconciliation | Original definition stamp, operation ID, conflict/unknown states; preserve live typing and opening generations |
| `src/App/CanDoItAll.Web/Api/ProjectsApi.cs:209` | POST saves ProjectEditorModel through SaveAsync and returns Guid | Mandatory conditional update and durable lookup/accepted result; documented error and compatibility mapping |
| `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentService.cs:85` | SaveProjectAsync -> lease -> Projects.SaveAsync | Narrow scalar command plus original definition expectation; same owner admission and authority |
| `ProjectStructureAgentService.cs:2386` | BuildProjectEditor initializes only scalar fields, leaving empty child lists | Confirmed static data-loss route: replaces existing collections with empty; must fix intent shape |
| `ProjectStructureAgentService.cs:1872` | Successful owner Guid is followed by summary read; missing readback throws | Carry known committed outcome instead of reclassifying readback failure |
| `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentContracts.cs:290` | Scalar save request has no definition expectation | Upgrade read/request/native tool bindings together; no server fresh-token refill |
| Project Structure native runtime tool provider/coordinator | Invokes ProjectStructureAgentService with existing source admission/journal | Link durable operation identity to owner receipt; inspect prepared invocation/hash compatibility before activation |
| Creation overloads in ProjectsService / ProjectCreationReceipt | Direct create, reserved create, root/child create, compensation | Separate create identity/replay; preserve exact reservation/lifetime and guarded inverse |
| Hierarchy methods in ProjectModels | Add/remove/reconnect under hierarchy/project keys | Separate graph/edge expectations where required; do not falsely fold unrelated hierarchy into definition stamp |
| DeleteCoreAsync in ProjectModels | Project gate, participant prepare/commit, retirement, cleanup | Retain operation dedupe evidence; confirmed delete/cleanup distinction; original lifetime |
| ProjectsProfileTransferStore | Owner-only maintenance session | Fresh local stamps and inert provenance; receipt retention/replacement rules |
| `src/App/CanDoItAll.Web/DevelopmentDiagnosticsEndpoints.cs:386,442` | Development creates through SaveAsync | Verify create-only setup remains valid; any update fixture must supply original expectation |
| Tests, sandbox, direct C# call sites | Owner services / fake draft acknowledgement | Update positive callers as well as rejection cases; no default nullable bypass to keep tests green |

Repository-wide `Set<Project>`, `Set<ProjectPhase>`, `Set<ProjectOptionSelection>`,
and `Set<ProjectHierarchyLink>` search found runtime writes concentrated in
ProjectsService and ProjectsProfileTransferStore. Reads exist in owner query
services, admission checks, and file scopes. This is a scoped source inventory,
not certification against arbitrary raw SQL or external database maintenance.
Before enforcement, repeat caller/raw-SQL/ExecuteUpdate searches against the final
diff and record all enabled writer conversions.

## Proven code risks versus required experiments

Static source defects/semantic gaps:

- Ordinary stale definition replacement has no captured content condition.
- Scalar project tool adapter sends empty full-replacement child collections.
- Editor root/child reads do not share a coherent snapshot.
- Old postcommit search payload can apply after a newer save; no durable repair.
- SerializableMutationScope Begin catch and Dispose skip process-lock release if
  transaction disposal throws.
- ProjectSourceMutationScope can throw from source disposal after a confirmed SQL
  commit, and wrappers can throw during postcommit readback.
- Existing creation receipt/reservation does not supply ordinary durable result
  replay with request digest and accepted definition stamp.

Conditional risks requiring deterministic or measured proof:

- Serializable snapshot established before a waiting advisory lock must not
  allow stale content publication. Existing lifecycle test covers recreation;
  add ordinary content/current-source tests rather than asserting it already does.
- Process stripes can serialize unrelated projects/profiles. Measure wait/hold
  and throughput; no measured slowdown is claimed here.
- Per-key advisory statements and repeated authority reads can amplify remote
  database latency. Bound complete key sets, use owner batches, and instrument.
- Search-in-transaction changes availability and cost semantics. Measure and
  test full rollback before accepting it.
- Durable restore epoch is not established by inspected runtime generation
  fields. Inspect activation/restore ownership and implement/prove the fence.
- Receipt detail retention without compact replay denial could re-admit old
  checkpoints. Ratify horizon/namespace before a cleanup worker exists.

## Existing proof to preserve

`ProjectAdmissionSnapshotRaceTests` has two parameterized PostgreSQL cases
(single/bulk read) for a snapshot established while waiting for retirement and
recreation. It observes an advisory wait and requires the final `FOR KEY SHARE`
to cause SerializationFailure with no late resource write. This is relevant
existing code, not a test run from this investigation.

`CoordinatedDatabaseTransactionTests` covers shared commit/rollback, mismatched
profiles, retired frames, retained participant commands, and explicit InMemory
limits. `ProjectsOwnerPersistenceTests` covers owner/canonical mappings, retained
rows, separate profiles, and complete batched mutation reads.

`ProjectsEditorMutationTests` already exercises held acknowledgements, actual
child IDs, typing after submission, successor draft generations, and known
project facts surviving follow-up failures. Reuse those semantics; adding a token
must not regress them. Real browser and native tool boundary tests remain needed.

## Smallest defensible implementation slices

1. Infrastructure cleanup correctness: finally release owned stripes during
   initialization/disposal failures, with deterministic fault-injection tests.
   No new generic transaction framework or lock strategy change.
2. Owner definition extraction and condition contract: original aggregate stamp,
   coherent read, required update expectation, narrow scalar route, linear child
   validation, canonical migration and retained-data tests.
3. Durable accepted-result protocol and commit knowledge: trusted operation scope,
   transactional receipt, result lookup, replay/mismatch, postcommit cleanup and
   readback semantics, bounded storage and retention contract.
4. Search atomic participant and full API/tool/UI adoption: all enabled writers,
   exact accepted acknowledgement, explicit conflict/unknown behavior, real
   native tool and two-session browser proof.
5. Runtime material-input policy and lifecycle extension: existing source ports,
   strict/current or deliberate snapshot behavior, bounded replanning, approval
   binding, restore activation fence, performance and two-host proof.

Slices are review/commit boundaries, not permissions to claim a protected pilot
while unconverted writers remain enabled. The detailed design and acceptance
matrix live in
[`01-owner-transaction-design.md`](../../../../docs/architecture/concurrent-mutations/01-owner-transaction-design.md).
