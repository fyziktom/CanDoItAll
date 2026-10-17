# P04 Card B1: dormant basis capture implementation card

Status: root approved the source-only dormant implementation; candidate source is frozen for parent validation. No build, discovery, execution, query-plan or performance result is claimed. This narrows [Card B's reviewed material scope](P04-card-B-material-task-witness.md) to immutable projection/codec and coherent capture. It does not implement a publisher, validation-before-publication API, receipt, policy settings, tool/HTTP route, Agent/Process/Workflow producer or service registration. No production call path changes behavior until a later reviewed card explicitly invokes it.

## Smallest file/API surface

| File, relative to repository | Proposed change |
| --- | --- |
| `src/Modules/CanDoItAll.Modules.Projects.Contracts/ProjectDefinitionContracts.cs` | Add `ProjectDefinitionScalarSnapshot(ProjectDefinitionExpectation Expectation, ProjectDefinitionScalars Scalars)`, validating non-null parts. This is read evidence, not a save acknowledgement or a full definition with fake empty child arrays. |
| `src/Modules/CanDoItAll.Modules.Projects.Contracts/ProjectPortfolioModels.cs` | If needed, one exact stored-source-invalid error constant alongside existing definition error codes. Reuse existing NotFound/LifetimeChanged/DefinitionLimitExceeded codes for their actual meanings. |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectDefinitionReader.cs` | Add `Task<Result<ProjectDefinitionScalarSnapshot>> ReadScalarsEnlistedAsync(ProjectWriteAdmission expected, CancellationToken ct = default)`. Add existing `DbContextOptions<ProjectsDbContext>` and `CoordinatedDatabaseTransaction` constructor dependencies. Existing standalone `ReadAsync` behavior stays unchanged. |
| `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/Planning/ProjectTaskPlanBasis.cs` (new) | Internal immutable basis/task facts, descriptor/version, fixed v1 bounds and typed capture-refusal reasons in one cohesive contract file. Use existing `DatasetNamespaceId`, Project snapshot/expectation and `ProjectTaskExecutionSnapshot`; no project graph, mutable editor model, raw JSON or credentials in a basis. |
| `.../Planning/ProjectTaskPlanMaterialProjection.cs` (new) | Internal pure selection and strict selected-metadata decoding from bounded persistence read rows; no database, service lookup or public serializer converter. Builds immutable sorted facts and validates identities/bounds. |
| `.../Planning/ProjectTaskPlanBasisCodec.cs` (new) | Internal explicit v1 encode/decode plus material equality/digest. Strict finite grammar, fixed property names, required values, duplicate rejection and bounded input/output. No polymorphic runtime type serialization. |
| `.../Planning/ProjectTaskPlanBasisCapture.cs` (new) | Internal concrete PostgreSQL capture service and its private bounded query row shapes. No interface with one implementation, new coordinator, partial class or ambient dependency collector. |
| `tests/Unit/CanDoItAll.Tests.Unit/ProjectTaskPlanBasisTests.cs` (new) | Proposed 12 plain Facts below; no MemberData, dynamic cardinality or fixture. |
| `tests/Integration/CanDoItAll.Tests.Integration/ProjectTaskPlanBasisCaptureTests.cs` (new) | Proposed 10 plain Facts below using existing owned PostgreSQL environment/application and supported command/transaction interceptors. |

The inspected current read interface is `IProjectRecordQueryService` in Projects.Contracts. It has real CRM/Resources/TestLab consumers, a CRM sandbox implementation and multiple component/integration fakes. **Do not extend it** for this specialized enlisted read. `ProjectDefinitionReader` is already registered as a concrete scoped Projects service and has no directly constructed `new ProjectDefinitionReader(...)` callsites in current `src`/`tests`. Its new dependencies already exist in Projects composition. Workbench already directly references Projects; no new project references or Infrastructure→Projects dependency are needed. Both modules already expose internals to Unit/Integration, so the dormant capture can be constructed in those tests without production DI registration or test-only hooks.

Proposed internal capture signature:

```csharp
Task<ProjectTaskPlanBasisReadResult> CaptureAsync(
    ProjectWriteAdmission project,
    Func<ProjectWriteAdmission, CancellationToken, Task> requireCurrentRead,
    CancellationToken cancellationToken = default);
```

`ProjectTaskPlanBasisReadResult` has only `Captured(ProjectTaskPlanBasis Basis)` and `Refused(ProjectTaskPlanBasisRefusal Reason)`. Reasons are source missing/lifetime changed/dataset changed/invalid source/limit exceeded; operational SQL errors, programmer misuse, caller cancellation and authority rejection are not swallowed into an empty or current basis. The callback is an internal trusted integration seam, never a request-body delegate or an authorization flag. Card B1 installs no producer that supplies it. Before a real native/Agent adapter is enabled, that adapter must bind its actual current disclosure authority and prove revocation behavior. Process/Workflow source guarantees remain later activation gates.

`ProjectTaskPlanBasis` contains descriptor/version, dataset namespace, `ProjectDefinitionScalarSnapshot`, DB capture instant and an immutable task-facts collection. It deliberately contains no resolved policy, runtime lineage, operation ID, journal ID or approval: Card E binds this owner evidence to its durable decision admission. Scope/version are fixed by trusted code, not caller strings. All lists are copied once, IDs nonempty, keys unique/ordinal, and each fact has exactly Card B's selected fields. Parent key is observed context only.

## Projects-owned enlisted scalar read

The method requires an already entered physical owner transaction. `CreateEnlistedAsync` provides that checked connection/transaction/profile boundary; outside an active coordinated owner it fails. It does not start a new transaction, take P again or create a second independent read context. The caller must already hold the complete owner gate; no claim that `ForMutation` method naming alone enforces lock ownership.

Validate `expected.DatabaseProfileId` against `admissions.DatabaseProfileId`. Read the target ID/lifetime/revision and byte lengths first, without materializing name/description/objective/current-phase text. Verify original lifetime, nonempty revision, valid scalar enum/date shape and the existing immutable owner ceiling `ProjectDefinitionLimits.VersionOneMaximum.MaxFieldUtf8Bytes` (65,536). The four text fields are consequently at most 256 KiB in total. Only then materialize those four fields plus status/target date on the same enlisted transaction; return a new owner-bound snapshot. Do not accept caller-selected profile authority or substitute a newly observed lifetime. Workbench adds these bounded bytes to its aggregate capture budget. Keep the existing full definition reader/limits/API semantics unchanged.

Use parameterized `Database.SqlQuery<T>` projections, already used by Projects operation observation, or equally bounded translated EF queries. SQL identifiers are fixed owner table/column names, values are parameters. No raw SQL is exposed to Workbench and no Projects persistence type crosses the returned contract. The method's two reads are safe only under the declared owner gate; a meaningful caller test must prove it.

## Capture transaction and bounded query

Dependencies are existing `IDbContextFactory<WorkbenchDbContext>`, `ProjectDefinitionReader`, `ProjectWriteAdmissionService` (current profile identity), `CoordinatedDatabaseTransaction` and `DatasetActivationService`. Use `DatasetLifecycleStore.RequireActiveForMutationAsync(owner, binding, ct)` for the exact held shared lifecycle guard. This is read-only work using an exclusion protocol, not permission to mutate.

1. Validate nonempty original admission and callback, current configured profile, and the fixed v1 limits. Invoke current-read authorization and capture the active local activation binding before opening the transaction; no filesystem lease is held during SQL.
2. Begin PostgreSQL ReadCommitted; configure finite transaction/lock/statement timeouts before waiting. Initial dormant sizing candidates are 5 seconds per transaction and 2 seconds per lock/statement, subject to the Card B measurements before cutover. Refuse unsupported database providers explicitly; no InMemory approximation of relational proof.
3. Acquire exact lifecycle shared guard first, then the complete sorted P set (only the target project for this dormant capture; any later enrolled source-project keys must be unioned before acquisition). Use relational scope-lock helpers directly inside the owned transaction. No use of the existing Serializable factory and no nested reacquisition of P/B. Enter the existing coordinated transaction.
4. Read bounded Projects scalars through the enlisted owner seam. Capture DB `clock_timestamp()` after the gate, not transaction-start time before a wait. Read bounded Workbench candidates and selected facts below. The P/lifecycle fences stabilize both owners while ReadCommitted obtains current snapshots after waits.
5. Recheck current read authority before returning any copied facts, then commit the read-only transaction and dispose resources. Material facts/codec work are bounded; no model, provider, external asset, whole graph, pricing lookup or UI work is invoked. If disposal/commit fails, report capture failure: this read-only operation has no accepted-effect result to reconcile.

The metadata preflight query returns at most 1,001 non-system WorkItem candidates for the exact project, with IDs and `octet_length` values of every fetched text field/raw MetadataJson, including subtype; it returns **no raw text payload**. Check the 1,000 candidate ceiling and 64 KiB per-field / 2 MiB aggregate text ceiling (including the already bounded Project scalars) before payload fetch. Bound subtype itself before materialization as well. Null persistence text, duplicate identity or invalid row shape is invalid source.

Fetch only those bounded row IDs, with exactly the selected scalar columns plus raw MetadataJson; no tracked full entity, binding/reference join, marker string or graph load. Apply `ProjectStructureCanonicalTaskMutationPolicy.IsTask` to the bounded candidates using the existing trim/ordinal case-insensitive semantics. This avoids assuming imported subtypes are normalized or that database equality/trim matches C# Unicode semantics. The ceiling is intentionally WorkItem candidates, so a large non-task WorkItem population is a visible scope-limit refusal, not a hidden unbounded scan. No task count/array is accepted from a caller.

Preflight and payload queries share the owner gate; require identical candidate IDs/shapes at materialization as a defensive invariant. Query-cost limits remain a measured gate: LIMIT bounds returned rows/allocations, not all server work, particularly filtering on large projects. The preflight intentionally has no ordering: any 1,001 candidates proves overflow, while at most 1,000 returns the complete set; immutable facts are sorted later by ordinal key. Retain and inspect the actual plan before any new index. No total-COUNT/full-graph query is required merely to decide that candidate 1,001 exceeds the cap.

## Strict selected metadata and codec behavior

Current canonical creation writes a WorkItem family, Task kind and explicit execution state (`ProjectStructureCanonicalTaskCreationPolicy`). Existing metadata serialization is camelCase, case-insensitive on read, string enums, and omits null properties (`ProjectWorkbenchMetadata.cs:1027`). Preserve those actual stored conventions deliberately:

- Raw metadata must be a bounded well-formed JSON object with one non-null WorkItem object. Reject missing/null family, duplicate WorkItem or duplicate selected properties, invalid Task kind, malformed selected strings/dates/enums and invalid execution-state/timestamp combinations. Missing nullable due/actual-date members means null because the existing writer omits nulls; missing required description/kind/execution members is invalid source, not a fabricated default. Accept supported legacy enum numbers only when defined, alongside the current stored enum strings.
- Reuse `ProjectTaskExecutionStatePolicy.Validate` for selected execution invariants. Do not call broad metadata validation that mutates/validates excluded cost, estimates or bindings and silently expands the material scope. Well-formed unknown/excluded extension fields are neither copied into the basis nor hashed; malformed JSON remains invalid source even when it appears inside an excluded area.
- The basis codec has a separate strict v1 wire grammar: every specified field present, explicit nullable values, unique known properties, valid/nonempty identities, exact descriptor/version and UTC instants. It rejects unknown versions and invalid/null/default values; no fallback to the live source, empty lists or arbitrary serialization of domain entities.
- Encoded basis ceiling is 1 MiB. Validate character/UTF-8 limits before allocations, use a bounded encoding path, and reject before allowing a larger output buffer. Material digest/equality excludes `CapturedAtUtc` (a new observation of the same facts is equal), includes dataset/project lifetime and original Project revision, and sorts facts by ordinal key with occurrence identity. Canonical equality compares actual typed facts; a digest is not a substitute for exact evidence.

## Intended source-derived proof shape

The implemented source cardinality is **12 Unit Facts + 10 Integration Facts**, with no theories, data providers or skipped cases. This is a source-derived expectation, not runner discovery or a passing result. See [the source freeze record](../validation/P04-card-B1-dormant-capture.md) for exact filters and proof limits.

| New Unit Fact | Assertion family |
| --- | --- |
| Frozen facts detach from mutable input | No alias of raw read rows/list/editor metadata; exact captured strings preserved. |
| Scope identity and complete empty set | Dataset/lifetime/revision/occurrence/default/duplicate invariants; empty is valid only with complete owner binding. |
| Each selected field is material | Explicit finite mutation table covers every selected value; compare/digest changes. |
| Excluded facts stay excluded | Positions/markers/assignee/estimate/cost/unknown metadata changes neither leak nor alter the basis. |
| Occurrence and value ABA differ | New occurrence with same public key conflicts; same occurrence restored material values compares equal. |
| Canonical predicate covers stored spelling | Task casing/whitespace, non-task WorkItem and system-managed rows classified exactly. |
| Strict metadata distinguishes omitted nulls from missing required values | Actual writer null omission works; invalid/missing/duplicate material fields refuse without defaults. |
| Execution metadata uses canonical invariants | Defined enum forms and actual timestamps; illegal state/time combinations refuse. |
| Codec round trip is deterministic | Collection order/capture time/material digest behavior and byte-stable reconstruction. |
| Codec rejects malformed envelope | Version/descriptor/unknown/duplicate/null/invalid IDs and collections; no catch-and-empty path. |
| All byte/count bounds | Exact ceiling and one-over for candidates, fields, aggregate and encoded bytes with multibyte text. |
| Snapshot revision/namespace is material | Same tasks under another namespace/lifetime/definition revision is different. |

| New Integration Fact | Required real boundary |
| --- | --- |
| Enlisted scalar and task capture uses one owner transaction | Actual Projects and Workbench reads share physical connection/transaction, return exact immutable facts; standalone full reader remains usable. |
| Capture waiting behind a material writer sees its committed values | Deterministic held P, actual definition/task write commit, capture resumes with fresh state including an insertion into empty scope. |
| Capture fence prevents mixed definition/task read | Barrier after scalar read while real writer attempts P; captured result is coherent and writer completes after release. |
| Lifecycle maintenance precedes owner reads | Held lifecycle exclusion blocks capture; changed namespace fails before raw payload materialization. |
| Too many candidates stops before text fetch | 1,001 bounded metadata rows refuse; command/reader observation proves no raw payload query. |
| Oversized field/aggregate stops before text fetch | Synthetic multibyte source over limit; exact boundary accepted; no payload/private-field logging. |
| Original lifetime/profile and unavailable source stay distinct | Missing target, replacement lifetime and wrong configured profile refuse; no fresh-original substitution. |
| Malformed stored task metadata is invalid source | Real malformed/missing selected data refuses; valid excluded-only changes still capture as equal. |
| Enlisted reader misuse and unsupported provider fail explicitly | No entered transaction and mismatched owner binding cannot open an independent fallback; no InMemory concurrency proof. |
| Cancellation/authorization retirement releases capture guards | Cancel while waiting and reject at final authority check; successor can acquire P; no material facts returned after rejection. |

Tests reuse existing `CanDoItAllTestEnvironment`/`TestApplication`, canonical migrations and the parent's single owned PostgreSQL server/leases. No new container, host port, copied outputs, SQL-text production parser, reflection hook or source mutation solely for fault injection. Use actual supported EF command/transaction interception and controlled owner gates as existing tests do. This slice cannot close Card C writer/replay/commit knowledge or Process/Workflow currentness tests.

## Entry and closure gates

Root approved this API/file card before coding. Implementation stays dormant, with no capture registration/consumer and no material publisher. Source review checks no new dependency direction, mutable data leakage, unbounded preflight allocation, implied authorization, stale-snapshot reuse or disclosure after failed final authority. Parent alone builds/discovers/executes/static-validates after source freeze. Required performance/capacity measurements and native/Agent/Process/Workflow activation remain open even if these focused tests pass.

The final `requireCurrentRead` callback executes while P is held. Before activation, each real Agent/catalog adapter must prove its lock order: acquire a required source/read lease before calling `CaptureAsync`, hold it through capture, and revalidate under that held lease in the callback; alternatively, prove a nonlocking current-read observation. The callback must never newly acquire a source/catalog lease after P when source writers can hold that lease and wait for P. A trusted/local callback alone does not prove this safety property. Concrete adapter tests must exercise source retirement and competing P acquisition using the actual authority implementation. The current dormant capture tests do not establish this producer-specific guarantee.

