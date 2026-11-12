# P04 Card B1 dormant capture: source freeze

Status: source-only candidate in the managed `concurrent-mutations-runtime` engine checkout. No build, discovery, execution, portability scan, database probe or performance measurement was run for this slice. Main Stable source and its expectation manifest remain unchanged. This record adds no completion claim to the bundle.

## Exact implementation boundary

The approved [B1 card](../plan/P04-card-B1-dormant-capture.md) is implemented in seven production files:

- Projects.Contracts `ProjectDefinitionContracts.cs`: immutable scalar read snapshot; existing editor/save contracts unchanged.
- Projects.Contracts `ProjectPortfolioModels.cs`: one stored scalar source-invalid error code.
- Projects `ProjectDefinitionReader.cs`: bounded scalar preflight and payload on the existing coordinated physical transaction. Original profile/lifetime and retired lifetime are checked; existing standalone full reader behavior is unchanged. The caller must already hold the lifecycle/project gates.
- Workbench `ProjectStructure/Planning/ProjectTaskPlanBasis.cs`: immutable fixed-scope basis/task facts, explicit typed refusals and ceilings.
- Workbench `ProjectStructure/Planning/ProjectTaskPlanMaterialProjection.cs`: existing canonical task predicate, strict selected WorkItem fields and execution invariants. Excluded metadata is ignored after bounded well-formed parsing.
- Workbench `ProjectStructure/Planning/ProjectTaskPlanBasisCodec.cs`: explicit v1 DTO grammar with required constructor fields, unknown/duplicate rejection, nonempty identities, UTC timestamps, pre-deserialization task count, bounded input/output, material equality and digest.
- Workbench `ProjectStructure/Planning/ProjectTaskPlanBasisCapture.cs`: PostgreSQL ReadCommitted, 5-second transaction / 2-second statement and lock deadlines, lifecycle then sorted project gate, enlisted scalars, numeric-only candidate preflight before text fetch, final trusted local read-authority callback, read-only commit and disposal.

There is no registration, consumer, producer, publisher, operation receipt, model change, migration, project reference or source-policy setting. Workbench retains its existing reference to Projects; no persistence entity/context crosses the new Projects return contract. The authority callback is internal trusted code, explicitly excludes external/model/network work under the gate, and has no installed adapter. A future actual adapter must prove its concrete authority, revocation and lock-order behavior.

## Source-derived expected tests

| Assembly | Exact filter | Expected discovery / execution |
| --- | --- | --- |
| `CanDoItAll.Tests.Unit` | `FullyQualifiedName~CanDoItAll.Tests.Unit.ProjectTaskPlanBasisTests` | 12 / 12 |
| `CanDoItAll.Tests.Integration` | `FullyQualifiedName~CanDoItAll.Tests.Integration.ProjectTaskPlanBasisCaptureTests` | 10 / 10 |

The two new files contain exactly 22 plain `[Fact]` methods, no `[Theory]`, MemberData, ClassData, inherited test methods, skip or custom trait. Source count is independent of future runner enumeration. Parent must first build standard outputs, compare discovery against these counts, then execute those exact outputs under the repository validation rules.

Unit coverage includes detached immutable data, complete empty scope and duplicate occurrence/key rejection, every selected task field (including actual timestamps independently), excluded metadata, occurrence versus current-value ABA, canonical spellings, strict metadata and execution state, deterministic round trip, malformed/unknown/missing/null/duplicate wire values, default observation time, namespace/lifetime/revision and each scalar. Size cases cover exact and over-limit multibyte fields, candidate count, real aggregate construction, exact 1 MiB encoded output and one byte over.

Integration source uses the existing isolated TestApplication/PostgreSQL lease. It checks the same connection/transaction for Projects/task reads; actual held project gates with later commit/insertion; a barrier between scalar and task reads; actual PostgreSQL lifecycle lock waiting and namespace mismatch; candidate/field/aggregate rejection before tagged payload queries; missing/original lifetime/profile behavior; invalid/excluded stored metadata; no-owner/alternate-profile/unsupported-provider refusal; and cancellation/final-authority rejection followed by successful successor capture. The lifecycle wait is observed through live `pg_blocking_pids` for the exact capture and holder backends; the EF interceptor captures only the backend identity at deadline configuration, not the raw ADO lifecycle command. The [bounded observation repair](../reviews/P04-B1-lifecycle-wait-observation-repair.md) replaces the transaction-cached `pg_stat_activity` poll. Race tasks are bounded and drained before fixture disposal.

## Independent source review and remaining gates

The runtime/policy agent reviewed the implementation read-only. Its two findings were corrected before freeze: reject default capture timestamps, and prove aggregate overflow using real individually valid facts. Its alternate-profile test correction is applied through `RequireOwnerProfile` on an unopened alternate database configuration. No new database is provisioned for that assertion. It reported no remaining production blocker in transaction freshness, lifecycle/project order, bounds, immutable projection or dormant module boundary. This is source review, not execution evidence.

Still open before any native/Agent activation:

- Parent compilation, discovery, focused execution, portability/static and applicable architecture checks; normal provider/serializer mapping remains unexecuted candidate behavior.
- Real query plans for large task/non-task/system populations. `LIMIT 1001` bounds returned allocations, not scanning/filtering work. Root source review removed unnecessary preflight ordering: all candidates are returned for an admissible set, and any 1,001 proves refusal; the basis still sorts facts canonically. No query or test depends on a partial page order. This is a source-level simplification without a measured speed claim; no speculative index was added.
- Measured capture p50/p95/p99, allocation, SQL/lock duration, multibyte payload and 1,000-candidate tails; disjoint/hot-project contention and timeout behavior. The fixed dormant deadlines/ceilings are candidates, not a capacity claim.
- Card C material publisher, complete writer-fence proof, known commit/replay behavior and durable decision binding; no definition acknowledgement is reused as task effect evidence.
- Actual trusted disclosure adapters, policy/runtime producers and Process/Workflow source-authority guarantees. Capture alone cannot promise an authorized publication, external-resource availability, assignment/estimate/cost/schedule feasibility or change-history equality.
- Concrete adapter lock order: the final `requireCurrentRead` callback runs while P is held. An Agent/catalog adapter must not acquire a source/catalog lease there when writers can hold that lease while waiting for P. The caller must acquire its necessary source/read lease before `CaptureAsync`, retain it through capture, and have the final callback revalidate under that already-held lease, or separately prove a nonlocking observation. "Trusted local callback" does not establish this ordering. Activation tests must hold the real source lease and P in their canonical order, race source retirement, and prove no reverse acquisition/deadlock or unauthorized disclosure. The dormant tests prove bounded capture and callback refusal only; they do not prove a real Agent/catalog authority adapter.
