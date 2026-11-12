# Closed inherited startup failures

Status: accepted before implementation. The original-input checkpoint qualifies 96
behaviors. Executor activation remains gated.

## Failure and authority boundaries

Fresh Process child enrollment already reserves one durable allocation. Only the
producer's acknowledged `CommittedNow` result permits fresh preparation; duplicate or
unknown acknowledgement uses ExistingOnly and must never start missing work. Core's
run lease serializes local preparation. No journal exists until preparation, initial
business binding and original-source checks have completed.

A failed lookup, missing file, timeout, lost save acknowledgement or interrupted process
does not prove zero work. Preserve reconciliation for these cases. A narrower useful
case can be proved: the actual local preparation callback returned or threw, no run save
was attempted, elapsed work is valid, and the owned run lease released successfully.
Core may then issue a sealed, immutable, nonserializable-inbound failure capability.
It carries the exact requested child, Agent, owner, workspace, profile, parent envelope,
original allocation, actual start/end times and measured work. It never grants read,
dispatch, approval or publication permission.

Measure the maximum of monotonic and UTC elapsed time, consistently with successful
initial preparation. Capture the end before lease release; mint the capability only
after successful release. Negative/invalid clocks, failure before measurement begins,
failed cleanup and any attempted save retain the original exception and no capability.
Keep the actual preparation exception as the inner exception of the typed failure;
do not expose its text to public result summaries. Charge an overrun without clamping.

The capability is issued only through the internal workspace admission entry point.
The public lower-level delegate-based admission API cannot mint it. Its identity is
captured from the workspace request before startup, so a failure before a candidate
exists cannot substitute a different parent. There is no fabricated execution-created
timestamp, business target, authorized journal, receipt or source claim.

## Ownership and dependency direction

Core owns measurement and the sealed failure capability. It has no Process dependency.
The driver-neutral Process result contract carries one explicitly ignored-for-JSON,
typed owner-evidence wrapper. Adapter-to-strategy conversion preserves that reference;
wire serialization cannot reconstruct it. No object bag, dynamic dispatch registry or
module-to-module dependency is introduced.

Application already loads and validates original child/material/budget/source selection
before reading an owner outcome. Extend that existing result selection with the original
accepted Agent identifier and original Project admission from its already loaded metadata;
do not issue a new query or read a current repaired assignment. A distinct live-outcome reader contract receives
that validated selection and the evidence. Ordinary durable outcome reads retain their
current contract. Unsupported live evidence is an explicit refusal.

Workbench is the registered business owner. It requires its exact sealed Core capability
type, the exact canonical parent envelope/allocation, original Agent, supported contract,
workspace and current profile identity/generation. It first reads stable historical
storage. Only a positively observed missing run can use the capability. Existing,
incomplete or unstable durable evidence remains reconciliation; a live failure cannot
override a journal or charge only part of competing work.

The actual closed initial interval has owner work revision one. Its outcome is Failed
or Cancelled, with NoEffect and the measured work. Successful strategy claims cannot
turn that evidence into successful publication. The existing Process result coordinator
atomically closes the child and settles its allocation. Its exact completed-command
receipt is checked before owner evidence, allowing recovery after acknowledgement loss
without serializing the transient capability. A crash before settlement remains pending.

## Alternatives and cost

Persisting a preparation state before every startup would add a file write to every
successful execution, change initial background-journal admission's new-run fence, and
require every recovery/ordinary-resume path to understand an unadmitted run. A separate
sidecar ledger duplicates the existing Process allocation owner. Persisting only failures
still introduces an unadmitted execution shape and its global resume guards. None is
needed to close the narrow, known local failure; crashes and unknown work deliberately
remain conservative.

This choice adds no database table, migration, durable file, extra selected SQL query or
happy-path persistence operation. Evidence is one immutable object for one failed
attempt. It is not retained in a global registry. Reuse the existing result and outcome
pipeline; do not grow the ordinary executor or add runtime partials.

## Proof and activation criteria

- Real Core startup faults before a candidate, during binding and original-parent checks;
  cancellation and overrun; actual elapsed work; no provider or tool dispatch.
- No capability after pre-start failure, invalid clocks, cleanup failure, successful
  save, failed save or lost save acknowledgement. Preserve original/aggregate errors.
- Original identity mismatch and supplied arbitrary/JSON evidence cannot settle work.
  Existing or unstable historical records cannot be overridden.
- Original Agent comes from accepted metadata after current assignment repair, with no
  added query or prompt projection.
- Adapter/strategy/snapshot forwarding keeps evidence in memory; JSON drops it. Exact
  receipt replay succeeds without it after atomic settlement and full-host restart.
- Concurrent fresh/duplicate producer invocation starts preparation only for the
  acknowledged fresh enrollment. Repeated calls cannot gain a new grant from a missing
  journal.
- Direct affected Release builds, declared test discovery, full portability enforcement,
  source/runtime identity, architecture comparison and owned-resource reconciliation.

The seven-project Core/Workbench/Process baseline has 2,661 types, 21,986 members and 798
existing findings. Review changes against it and the preceding Persistence baseline.
No new project/package reference, runtime partial or cycle is allowed.

The first implementation review adds seven cohesive types and two informational member
count findings: the immutable failure capability has twelve source members and the
admission coordinator has nine. The former carries one exact attempt's identity and
measurement with no behavior; the latter still owns only serialized admission and cleanup,
with measurement state isolated in its helper. These findings are reviewed, not hidden by
splitting protocol properties or introducing more forwarding types.

This checkpoint does not remove the executor guard. Capacity, actual adapter activation,
manager continuation, cancellation/refresh, Workflow and settings/SPA remain separate
requirements. SPA stays on its existing branch without merge.
