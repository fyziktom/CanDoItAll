# SB01 Focused EF Core Query Review

## Agent provider/settings path

`WorkspaceBackedAgentProviderProfileRegistry`:

- creates an `AppDbContext` per call through `IDbContextFactory`;
- uses `AsNoTracking` for read-only list/single lookups;
- filters by provider primary identity and returns at most one row for startup;
- falls back to the file catalog when EF has no row.

The EF shape itself is appropriate. The startup problem is orchestration: provider fallback can reread the same file catalog already loaded by the caller. The planned startup aggregate must supply that catalog to fallback resolution before provider/session reads are allowed to overlap.

`FloatingAgentChatSettingsService`:

- creates its own context;
- uses `AsNoTracking` for the read;
- filters by one stable settings ID;
- caches `JsonSerializerOptions` statically.

No query optimization is justified without a measured issue. Settings initialization may overlap independent metadata warmup only because each operation owns its context and failure precedence is explicit.

## Process projection path

Inspected `EfProcessProjectionStore`, `EfProcessRunRecordStore`, runtime-event/assignment stores, `ProcessRuntimeProjectionQueryService`, and `ProcessWorkspaceShellProjectionService`.

Positive:

- read queries use `AsNoTracking`;
- projection/history/event reads apply explicit bounds (`Take`) and ordering;
- run-record list paths project summary columns;
- live process queries bound overscan/enrichment/history/telemetry;
- `PreviouslyLoadedRuns` already avoids the initial live-snapshot reread;
- no lazy-loading N+1 pattern was confirmed.

Concurrency constraint:

- process stores are scoped services over the same scoped `ProcessPersistenceDbContext`;
- the shell/runtime query chain must not use `Task.WhenAll` across those stores;
- making reads parallel would first require factory-created independent contexts and a coherence/failure policy, which is unnecessary for this initiative because Manager chat can consume the held shell projection.

The correct optimization is zero-query snapshot reuse for chat, not parallel repetition of process queries.

## Project Structure assembly path

`ProjectStructureAssemblyService.LoadAsync` uses one caller-owned `AppDbContext`, loads canonical nodes/bindings/links/layout, and runs all projection contributors sequentially. Some canonical entities are deliberately normalized then reset to `Unchanged`; converting this method mechanically to no-tracking would be a correctness change.

The assembled surface can also cross process persistence and filesystem/project scans through contributors. `FindNodeAsync` and agent read methods currently rebuild the whole assembly before filtering.

The safe optimization is:

- build a pure immutable snapshot from the already-held surface;
- verify the mapper performs zero context creation/query calls;
- use that exact invocation snapshot for read tools;
- keep writes on canonical services with expected revision.

## Validation ownership

SB01 establishes orchestration-level call counts only. Its `provider-gets` metric counts `IProviderProfileRegistry.GetProviderAsync` invocations; it is not an EF command count and must never be presented as one.

Required for A1:

- Count catalog/provider/session/run-detail operations before/after.
- Confirm by source inspection that the current EF provider primary lookup is no-tracking, keyed, and bounded.
- Record the exact later-gate tests needed for snapshot and query behavior.

Required before A5/backend-performance closure:

- Capture provider EF command count with a non-sensitive interceptor/logger and verify a single keyed query on the primary path.
- Assert project snapshot mapping creates no `DbContext`.
- Assert Process Manager send performs no process projection query when a held snapshot is present.
- Instrument process context instances if any new parallelism is proposed; overlap on one instance is a test failure.
- Check EF logs for client-evaluation warnings and query count; do not enable sensitive-data logging in captured proof.

## Decision

No compiled query, raw SQL, new include strategy, or broad EF refactor is currently justified. The material gain is avoiding queries/reassembly, preserving no-tracking/bounds, and preventing unsafe shared-context parallelism.
