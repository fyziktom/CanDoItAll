# P28: refresh capture preparation and progress queries

Status: Pass for all 33 selected cases. Behavioral proof tier. This checkpoint implements the
bounded preparation boundary preceding the capture producer; it does not activate public
refresh, a worker, automatic replan or interrupted-capture retry.

## Ownership and decisions

`ProcessDecisionRefreshCaptureIdentity` derives eight distinct stage/allocation/material
identities from the original admission and explicit stage values. The successor reuses its
existing identity. Budget revisions, timestamps and fresh requests cannot create a new
attempt. Fixed identity vectors and restart reconstruction protect persisted identities.

The Application query contract has two reads. Progress selects at most six known command
receipts in one indexed Persistence query, without loading the runtime state, history,
accepted input or material payload. Preparation separately reuses original admission,
accepted-input, result/child and root-scope readers. It verifies exact reconciled NoEffect
accounting, current head, original source/profile, local/root funding and both ceilings.
It exposes current root status, local pending work and root pending work without authorizing
capture. The UOW still owns mutation validation under the root fence.

The successor guard and preparation query share one internal reconciliation reader. The
existing child reader validates the local budget's parent and the matching funded child
allocation; the new root admission check retains the same root policy. No source reader,
capturer or provider runs inside the Process transaction. No new project reference,
persistence schema, business partial, service locator or independent write transaction
was added. Composition registers the query at the existing Process module boundary.

Root inheritance needs the retained material stamp and timestamp. Neither preparation nor
progress reads its content payload. These are bounded-read properties, not throughput or
large-history benchmark claims. The general idempotency reader remains unchanged.

## Selected proof and invalidation

Expected discovery is **33 cases**: three Unit capture-identity cases, eleven Integration
capture-query cases, and nineteen Integration successor cases. Exact filters are
`FullyQualifiedName~CanDoItAll.Tests.Unit.ProcessDecisionRefreshCaptureIdentityTests.`,
`FullyQualifiedName~CanDoItAll.Tests.Integration.ProcessDecisionRefreshCaptureQueryTests.`
and `FullyQualifiedName~CanDoItAll.Tests.Integration.ProcessDecisionRefreshSuccessorTests.`
Zero/unexpected discovery is rejected. The successor class is the affected dependent flow
because its reconciliation reader was factored; unrelated owner/accounting code is unchanged.

Changed Application/Persistence/module source and these test inputs invalidate the selection.
Direct production builds cover Application, Persistence and Modules.Processes; both test
projects refresh standard Release outputs with local libraries. No schema, public API,
shared serialization, global composition or build configuration changed, so no unfiltered
suite was selected. Full-engine E2E and the earlier stable-suite failures remain open.

The first Integration build caught a test helper passing a runtime command ID where the
refresh API requires a distinct action ID. The helper was corrected before execution;
the original failed build remains in the proof directory. Production builds and the
corrected Integration build pass. Discovery confirms 3/11/19 cases. The frozen candidate
contains 1,273 source pins and 44 standard Release output hashes.

The first execution passed all three Unit and nine query cases. Two query assertions
incorrectly required the root to be Blocked; the real fixture retains an Active root with
a blocked step. The assertion now compares the returned root status with the exact original
state. Source/output verification ran before that repair. The verified comparison proves
only this Integration test source and its DLL/PDB changed; production and Unit binaries
are unchanged. All 33 cases were rediscovered for the corrected execution.

## Architecture review

Before: `snap-20261023182501-fb03b32e`; after:
`snap-20261023192129-17b44d93`. The same three intended project edges and zero scoped cycles
remain. The collector still reports 189 WS0002 errors; direct builds are the compilation
evidence. Two new member-count findings concern the immutable stage identity and selection
records. They retain one identity domain and one cohesive IO result respectively; splitting
them merely to silence the metric would add indirection. No new complexity-method finding
was introduced. The after scope has 315 documents, 928 types and 7,664 members.

## Proof and resource ownership

Ignored proof root:
`.artifacts/concurrent-mutations/validation/c4-process-refresh-capture-query`.
The corrected run uses its `-verified` sibling, also declared before creation.
Its sibling `.ownership.json` was written before directory creation. Helpers retain exact
build/discovery/test commands, logs, expected case names, source/output identities, resource
observation and final static checks. The existing isolated PostgreSQL 18 fixture on 53619
is reused. The ordinary 5032 instance, SPA colleague-review branch and installed tools are
preserved. The corrected run passed all 33 cases with zero skipped or unexecuted selected
cases. Final no-write portability enforcement passed: 10,129 files, 47,425 findings,
15,697 unchanged reviewed executable allowances. The six new C# sources have no scanner
findings. Tool checks (6/4/7), rendering registration, documentation and whitespace pass.
All 1,273 source pins and 44 output hashes remained unchanged during validation. The
corrected observer covered all three commands for 451 seconds and reconciled zero new
processes, databases and temporary directories. Its two bulk scans were removed after
retaining compact summaries/hashes, making four removed bulk files across both runs.
Final secret review covered 85 scanner text files and six supplemental text files; the
three reviewed variable references contain no actual credential sentinel. The shared
isolated PostgreSQL fixture is retained for the authorized capture-producer checkpoint.
The first run reconciled zero new process/database/temporary-directory resources, passed
portability enforcement with 15,697 unchanged executable allowances, and removed its two
verified bulk scan files after retaining compact evidence. Secret review covered 86 scanner
text files and six supplemental text files; only the three known private-variable references
were flagged, with no actual sentinel values found.

Historical cleanup denials remain separate. In particular, automatic approval review
previously denied removal of
`C:/Users/lucys/AppData/Local/Temp/candoitall-api-tests/118b26c298ad4d6d9fea358f28134973`
with “blocked by policy.” This checkpoint does not retry that target or claim it is gone.

## Progression decision

The preparation/progress read boundary and stable identities may support the capture
producer. Query results remain advisory; the coordinator must qualify interleaved metadata
reads, bounded stale-state retries and the UOW's fresh mutation check before invoking a
capturer. Progress receipts do not themselves authorize another capture. The next producer
must prove actual source capture, successful successor advance, refusal/expiry/exception
settlement and cancellation draining. Interrupted unknown work and explicit retry identity
remain separate gates before public refresh or recurring delivery.
