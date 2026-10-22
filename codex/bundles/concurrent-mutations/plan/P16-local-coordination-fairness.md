# P16 — Local workspace admission fairness

Status: design selected after native failure and diagnostic evidence; implementation
and acceptance are still required. This is a process-local scheduling repair, not a
distributed fairness guarantee or permission to activate the recovery worker.

## Evidence and responsibility

The [contention qualification](../validation/2026-10-22-recovery-contention.md) fails five
of six cases. In the diagnostic RunSnapshot/32-writer case, both roots remain at decision
revision 4 before the held key is released. Earlier attempts spend 1.86–1.96 seconds in
one `workspace.coordination.wait` and exhaust the unchanged two-second slice. JSON
deserialization in successful observed reads takes 2–6 ms. The later uncontended child
stop takes about 0.61–0.63 seconds. Inclusive activity durations overlap.

Each File store currently owns its own semaphore. Different stores in the same process
therefore compete by repeatedly opening the same exclusive workspace file, polling every
25 ms after failure. That cross-process exclusion protects correctness but provides no
local admission ordering. Repeated publishers can keep winning while recovery spends its
slice waiting and retries its unchanged durable position.

The File persistence adapter owns this scheduling concern. Keep the existing durable
file lock as the cross-process authority; place a shared, cancellable local admission
queue in front of it. No Process, Workflow, SQL, HTTP or UI policy belongs in that queue.

## Selected design

Add one internal `FileSandboxWorkspaceCoordinationGate` in Persistence. Instances identify
the normalized absolute workspace lock path. A private pool shares active queue entries
across store instances for that spelling and removes an entry after its final holder and
waiters leave. No cache, timer, thread, database row, filesystem marker or DI registration
is introduced. The short pool monitor only changes queue bookkeeping; no I/O or callback
runs under it. Queued continuations run asynchronously.

Use two typed classes of admission, ordinary and control. Within each queue, preserve
arrival order. Serve at most eight consecutive control admissions when ordinary work is
waiting, then one ordinary admission. A cancelled queued request is removed without
consuming a permit. Cancellation racing with a grant leaves the successful recipient
responsible for disposing its lease, matching normal semaphore ownership. Lease release
is idempotent and must never release another holder's permit.

Map selected run/detail/admission reads and Process execution-page reads to control
admission. Also map the existing mutation overload that rechecks current authority after
publication-key waits to control admission. Its production caller is the parent-stop
protocol; source search finds the File implementation and one unit fixture. Make this
mapping explicit in that overload, not in callback inspection, an ambient flag or an
application-specific dependency. Other publication and bulk operations remain ordinary.
The bounded burst protects their progress when control readers remain active.

`FileSandboxWorkspacePublicationScope` keeps owning the local lease and workspace file
lease together. It releases both before selected-key or external-authority waits and
reacquires with the operation's original admission class. Sorted publication keys,
original-witness validation, SQL authority, durable journals and acknowledgement rules
are unchanged. No public contract, project reference, partial class or replacement owner
is needed. The existing store delegates scheduling; the queue is independently testable.

## Rejected alternatives and limits

- Raising the two-second deadline hides the observed admission starvation and increases
  the time one held child occupies the worker.
- A single ordinary FIFO queue still puts a control operation behind an entire large
  writer burst. Selected cancellation needs bounded opportunities between publications.
- Priority inferred from cancellation tokens or ambient Process context would hide
  behavior and couple persistence to its caller. Explicit local admission classes avoid it.
- Reading original journals without their existing workspace guard would change pending
  publication and multi-file consistency semantics. This repair does not weaken them.
- Moving file scheduling into Process or Infrastructure-to-Module callbacks reverses the
  dependency direction. The existing Persistence owner is sufficient.

The queue cannot preempt a holder, shorten a durable flush, solve history-sized index
rewrites, or schedule another OS process. Different path aliases still rely on the
original file lock for exclusion; the local queue does not claim physical alias discovery.
Cross-process contention and sustained capacity remain separate qualification gates.
The default worker remains disabled until its complete activation contract is qualified.

## Validation and architecture gate

Reuse unchanged-source snapshot `snap-20261022222045-198deb42` as the Persistence baseline:
148 types, 1,536 members, 55 findings, and the recorded 183 dependency edges. Its existing
NuGet/load limitations remain explicit; direct builds are authoritative. Compare a scoped
snapshot after implementation and inspect any added dependency/cycle finding. No project
or service registration change is planned.

Pure tests must prove same-path exclusion across instances, unrelated-path independence,
FIFO within each class, the eight-control burst limit, cancellation before/while queued,
grant/cancellation races, and idempotent release. They must not create files or a runtime.
Adapt the existing publication-budget test to verify admission availability by acquiring
a lease, preserving its actual file-key timeout and release assertions.

Refresh affected production and consumer assemblies in standard Release, then run the
six original contention rows without relaxing their assertion or deadline. Re-run the
single diagnostic to verify where time moved. Include existing publication-budget,
native File/SQL deadline, selected-authority, parent-stop and completion regression lanes
with exact discovery established before execution. Preserve all initial failures and
resource observations. Check ordinary write cost and progress as well as control progress;
do not trade a hidden starvation defect for a passing cancellation test. Full portability
and final no-write enforcement remain mandatory; broad Stable waits for the integrated
freeze. No activation or HTTP cutover follows from a merely compiling queue.
