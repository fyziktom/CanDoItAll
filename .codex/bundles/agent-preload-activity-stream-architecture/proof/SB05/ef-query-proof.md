# SB05 EF Core and Query-Count Proof

## Evidence boundary

This review combines exact source inspection with the confirmed final validation
handoff. The provider command counts below are confirmed results, but the original
console/SQL transcript was not retained. They must not be described as raw SQL logs.
The process count is a source-derived estimate for the selected projection shape,
not a universal or statistically measured count.

## Provider snapshot path

`DatabaseProviderRuntimeProfileSnapshotLoader` creates and disposes one
factory-created `AppDbContext` per database operation.

| Scenario | Confirmed SQL count | Shape | Classification |
| --- | ---: | --- | --- |
| Warm selected provider | 1 | Scalar `ConcurrencyToken` projection keyed by provider ID | Expected validation cost; no entity materialization |
| Synthetic fallback provider | 0 | Immutable in-memory fallback lease | Correct zero-SQL special case |
| Remotely changed provider | 3 | Revision probe, fenced refresh probe, keyed provider reload | Bounded change-recovery path; not N+1 |

Source proof:

- `LoadRevisionAsync` uses `AsNoTracking`, filters by the provider primary identity,
  projects only nullable `ConcurrencyToken`, and calls `SingleOrDefaultAsync`.
- `LoadAsync` uses `AsNoTracking` and one keyed `SingleOrDefaultAsync`.
- `LoadRevisionsAsync` uses `AsNoTracking` and projects only provider ID plus
  concurrency token.
- the synthetic fallback returns before a context is created.
- warm execution does not call `LoadAllAsync`; it performs the one scalar revision
  validation before returning the immutable lease.

The three provider snapshot captures reported by the startup harness are in-memory
O(1) validation/fencing reads. They are not three EF queries.

## Process live-projection path

The reviewed selected live query uses `TakeRuns: 10`. Its expected relational shape
is approximately eight SQL commands:

1. one bounded projection-snapshot query;
2. six split-query commands for runtime state plus its five included collections;
3. one batched assignment query.

`EfProcessRuntimeUnitOfWork.BuildStateQuery` uses `AsNoTracking` for reads and
`AsSplitQuery` across the five collections, avoiding a cartesian product. Both
runtime-state and assignment APIs accept bounded run-ID batches. The final
`Live_process_enrichment_batches_runtime_state_and_assignment_reads` test proves one
`LoadManyAsync`, zero per-run state loads, one `LoadByRunsAsync`, and zero per-run
assignment loads for six runs.

Optional observation, history, or selected-detail features can add their own bounded
queries. Therefore eight is a documented estimate for the selected enrichment shape,
not a promise for every process workspace request.

## N+1, tracking, and client-evaluation review

- Provider validation is keyed and constant in provider-row count.
- Process enrichment batches states and assignments across selected run IDs.
- Read-only process persistence queries inspected use `AsNoTracking`.
- bounds are applied before materialization (`Take`, maximum batch sizes, selected
  run-ID arrays).
- no lazy-loading navigation access or per-run EF query loop was found in the
  reviewed path.
- no client-only method is introduced inside the reviewed EF predicates/projections.
  This is source proof; no claim is made that a retained runtime log showed zero
  client-evaluation warnings.

## Concurrency rule

Process application stores share a scoped `ProcessPersistenceDbContext`; the query
chain remains sequential. There is no `Task.WhenAll` or `Parallel.*` in the reviewed
projection path. Provider reads may be independently awaited only because each uses
its own factory-created context.

## Decision

Query shape passes A5. The provider path has explicit 0/1/3 bounded command counts,
and process enrichment is bounded and batched rather than N+1. No compiled query,
raw SQL rewrite, or broader tracking-policy change is justified by the evidence.
