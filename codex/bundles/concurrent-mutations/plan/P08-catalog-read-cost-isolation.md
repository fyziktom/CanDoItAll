# Isolate review observation and catalog read cost

Status: diagnostics completed before further production optimization. P08 execution
separation passes 67 correctness cases, but does not qualify read capacity. The
[checkpoint](../validation/2026-10-21-review-read-boundary.md) retains all results.

The replacement P08 candidate passed 70 of 73 cases. Two HTTP shapes exceeded one
second. RunSnapshot/128 recorded three samples before the Process SQL count rose
from 27 to 34. Its test-wide factory guard also rejected a background claim-recovery
worker. The retained 28 samples allocate about 79–84 MB per request; they do not
attribute those allocations solely to the request. Three shapes passed their local
threshold, which does not close the incomplete matrix or sustained capacity.

## Measurement correction

Scope the execution-factory guard, EF command counts and optional activity capture
to the actual selected GET through the host's existing HTTP context accessor. Use
the existing Process dispatch option to disable recovery only in these cost hosts.
Keep the ordinary host and all seven HTTP recovery correctness tests unchanged.
The next measurements establish a new isolated baseline; do not call differences
from the background-contaminated run a measured production speedup. Preserve both
snapshot modes, all three sizes, two warmups, five samples, the one-second limit,
exact observed identity, unchanged child revision, one provider call and zero Project
writes. Process allocation remains process-wide; that limitation stays explicit.

## Locate the remaining fixed cost

Managed stacks identify repeated default-seed building and catalog normalization.
The current file store normalizes every current catalog read and serializes both
catalog versions to decide whether normalization requires a write. Existing activity
evidence records four catalog reads and eight catalog serializations per review.
Do not infer allocation shares from inclusive CPU stack durations.

Add three synchronous diagnostics over the actual default catalog: seed creation,
normalization, and the existing equivalent-payload serialization comparison. Record
five warmed observations of elapsed time, current-thread allocation, Agent count and
catalog byte size. Check normalization idempotence and the actual result. Normalization
includes seed creation; these are overlapping component measurements, not disjoint
request costs. No new host, database, Docker stack or runtime project is required.

Default templates are external files resolved from the current execution roots.
Their manifests, member settings, skills, instructions and capability policies can
change. Reject a permanent seed cache, timestamp-only identity, skipping normalization,
or cross-request authorization caching. If measurements justify reuse, first design
bounded immutable values, complete input identity, invalidation on changed/missing
files, root separation and mutation isolation. Current authority must still read the
current catalog and all existing grants, profile, source and Project fences.

## Qualification

Build the changed Integration assembly in standard Release; production binaries
remain unchanged from P08. Discover three component cases and six HTTP cost cases,
run full portability enforcement, verify complete frozen source/runtime identities,
and observe/reconcile the existing fixture leases. Retain failures and partial
sample sets explicitly. Correctness from the unchanged 67-case P08 run remains
separate evidence; this diagnostic selection does not re-run it.

Review the component results before choosing any additional production change.
Repeat the identical isolated HTTP matrix after that change, plus the affected
storage/seed/authority regressions. Parent accounting recovery, cancellation/refresh,
executor activation, Workflow/settings/UI and final sustained/broad closure remain
open. SPA stays on `codex/concurrent-mutations` without a merge.

The three diagnostics pass: the 31-Agent, 734,165-byte default catalog allocates
5,925,256 bytes for seed construction, 13,130,488 bytes for normalization including
seeds, and 2,954,928 bytes for its equivalent-payload serialization comparison.
These are median synchronous component observations. All six isolated HTTP cost
cases fail latency after 30 complete samples, constant 27 request-scoped SQL commands
and all safety assertions. No new execution factory is created by the selected GET.
Bounded reuse of unchanged seed and catalog inputs is the next design decision;
the current evidence does not establish a production speedup or sustained capacity.
