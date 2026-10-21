# P07 — bounded governed launch payloads and finite root capacity

This design follows the retained 202-case closed-startup proof. Implementation proceeds
with the executor guard still enabled.

## Decision and ownership

Keep the existing 256 lifetime allocations and shared membership ceiling. These bound
history, not concurrent workers. Root capture, selected preparation and provider children
all consume slots. Settlement releases unused work, never history slots. Existing explicit
capacity refusals must reach a stopped/review outcome; no rollover, reset or replacement
grant is permitted. The 1024-step selected-read benchmark establishes query isolation,
not support for executing 1024 planning children from one root budget.

Processes.Persistence owns serialized launch size and accepted-source retention.
Introduce no schema, public execution policy, project reference or application coordinator.
Keep complete allocation/member validation at root capture/finish. Those operations
validate original identities, all sums, claims and unclosed children; aggregate counts
alone cannot replace that proof.

New preparations containing governed assignments are limited to 32 MiB of serialized
UTF-8 launch payload. Atomic accepted-source capture limits all projected source bytes
to 16 MiB, including header, metadata and decision inputs. Individual source/input bounds remain
1 MiB/4 MiB. These limits comfortably exceed the measured approximately 5.3 MiB selected
projection total at 1024 small steps and the 1 MiB neighboring-prompt case. They prevent
the theoretical multi-gigabyte retention permitted by independent per-input limits.
They are explicit finite first-release limits, not inferred launch topology constraints.

Ordinary preparations keep their existing behavior. Historical prepared bytes and exact
receipts are read under their existing versioned rules; applying a new preparation limit
must not invalidate an already accepted receipt. A previously prepared governed launch
that exceeds the accepted projection limits is refused atomically at first acceptance.
No prompt truncation, partial launch, implicit migration or fallback is allowed.

## Minimal implementation

- Use a private synchronous bounded buffer in a small Persistence JSON serializer.
  Check each write before retaining bytes. Preserve the same serializer options and
  exact output bytes. The private Stream implementation is an adapter to System.Text.Json,
  not a new runtime architectural boundary. It owns no domain behavior.
- Use that serializer for new governed preparation. Leave existing individual source
  envelopes and ordinary full preparation on their current serialization paths; their
  existing per-envelope limits remain enforced. This avoids adding an output buffer
  allocation to every small accepted-source envelope.
- While capturing accepted source, count actual UTF-8 bytes before retaining each metadata
  or input envelope. Include header and metadata in one total; do not only count prompts.
  Keep that explicit total inside the existing cohesive codec. An independent aggregate
  input limit would add no protection beyond this complete projection bound.
- A limit failure is an explicit InvalidDataException with a fixed non-sensitive message.
  It occurs before source/database mutation during preparation, or rolls back initial
  acceptance through the existing runtime transaction.

The retained output limit is not a bound on the caller's already allocated object graph,
serializer temporary buffers or total process memory. A single rejected large input may
still allocate serializer temporaries. Do not claim universal memory isolation.

## Capacity experiment and acceptance

Use real owner commits to construct 1, 32, 128 and 255 shared members. With one root
capture, 255 zero-effect memberships fill all 256 allocation slots. The synthetic
zero-work settlements isolate accounting shape; they do not represent provider cost.
Each member must be complete and every claim released before root finish.

Measure setup separately from the actual owner finish transaction. Capture query count,
actual selected query plans/row counts, elapsed time and process allocation. The full
allocation/member reads may scale to the declared ceiling; hot selected operations must
remain indexed and must not load neighboring prompt/material payloads. Preserve existing
corruption, open-work, duplicate and recovery tests. Do not increase limits to pass.

The finite host reference requires each measured finish transaction to complete within
one second; setup is measured separately. This is an explicit single-sample reference,
not a percentile or sustained-throughput claim. Also compare ordinary and bounded full
preparation serialization on identical 1/128/1024-step and large-neighbor shapes.

Accept finite-cost evidence only. Sustained same-root/independent-root contention,
fairness, host cancellation and end-to-end provider/manager behavior remain separate
release gates. An unresolved cost regression keeps activation gated.

## Testability and validation

The bounded serializer must be testable without an application host: exact byte
equivalence, empty/small boundary, escaping/non-ASCII, large write refusal and no silent
truncation. Codec tests cover individually valid inputs overflowing aggregate limits,
metadata overflow, unchanged ordinary preparation and retained historical reads.

Real PostgreSQL tests prove no partial preparation or acceptance and preserve exact
accepted-source replay. Re-run affected accepted-input/source and shared-scope/funding
tests plus the four selected-input measurements to expose a serialization regression.
Declare discovery counts before execution. Build Persistence and the owning tests in
standard Release outputs, serialize builds, run fresh full portability enforcement,
compare the scoped architecture baseline, verify frozen sources/runtime and reconcile
owned fixture resources.

The five-project baseline is snap-20261021053914-77361fd7: 2304 types, 19922 members,
112 registrations, 32 mapped entities, 760 findings, seven project edges and six existing
cycles. No new reference, runtime partial or cycle is permitted. Existing analyzer
diagnostics prevent a claim of a completely healthy full solution graph.

Executor activation, actual adapter integration, manager completion, cancellation/refresh,
Workflow, persisted settings/SPA, remaining writer cutovers and final broad closure remain
required. SPA stays on codex/concurrent-mutations without merge.
