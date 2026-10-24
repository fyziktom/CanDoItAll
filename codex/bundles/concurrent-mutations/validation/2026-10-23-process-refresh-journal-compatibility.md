# Actual older-producer refresh journal retention

Status: all ten cross-binary behavioral cases, final restoration and static checks pass.
Temporary junction/checkout cleanup remains blocked after the user-authorized retry was rejected.
This qualifies the specified historical
read/replay paths; it does not activate delivery, retry or public refresh/Replan.

## Entry and ownership

Candidate: signed `e906b22adb6b2998c88a79b410094c6cba234eae`. Actual previous producer:
signed `e46625b5af9677ede3bb9a0f93c0139b0822dff9`. Both use standard Release outputs and
the current local Components/FileTools dependencies. The previous checkout contains no
persistent capture-journal consumer. Its production sources and project references were
not modified. The [P28 entry and cancellation handoff](../plan/P28-process-refresh-recovery.md)
were reviewed before changing the temporary probes. Behavioral entry gate passes: the
storage prerequisite, ten explicit scenarios, owner boundaries and invalidation keys are
defined. A test-only handoff does not invalidate unrelated suites or require another
unfiltered gate. The SharedInfo .NET/tooling standards and repository Testing/current CI
instructions govern standard outputs, bounded commands, fixture reuse and static closure.

The existing runtime and cost-baseline worktrees remain preserved. Managed creation with
the longer name failed on a Windows filename limit and left no checkout. Managed `jrc`
creation succeeded; its first build then failed copying six deeply nested template files.
A recorded short junction to the same checkout allowed the build to use its existing
Release bin/obj outputs without a source/configuration change or another output graph.

## Actual behavior

The candidate uses `ProjectTaskPlanReviewFixture`, the registered Workbench capturer,
real refresh admission/accounting and the journal/UOW owners. Explicit interruption
points retain real Reserved, Invoking and Returned states. Settled includes the original
successor, and Legacy uses the existing reservation-shaped request without a journal.
Each fixture keeps its isolated database lease while the previous binary runs.

The historical probe uses the actual older `EfProcessRuntimeUnitOfWork.LoadAsync`,
`EfProcessDecisionRefreshCaptureQuery` and `ProcessDecisionRefreshCaptureCoordinator`.
Its other readers are the actual older EF owners. A forbidden commit port counts and
rejects any attempted mutation; no capturer is provided. Database profile information is
passed privately through the child environment, never command arguments or evidence files.
The command handoff explicitly reconstructs the old typed IDs and asserts its exact JSON
round trip before calling the production owner.

| Retained state | Snapshot modes | Older outcome | Journal rows: Run / Step | Payload rows |
| --- | --- | --- | --- | --- |
| Reserved | RunSnapshot, StepSnapshot | Pending | 1 / 1 | 0 |
| Invoking, acknowledgement lost | RunSnapshot, StepSnapshot | Pending | 1 / 1 | 0 |
| Returned, acknowledgement lost | RunSnapshot, StepSnapshot | Pending | 1 / 1 | 1 |
| Settled, successor committed | RunSnapshot, StepSnapshot | Advanced | 2 / 1 | 0 |
| Legacy reservation, no journal | RunSnapshot, StepSnapshot | Pending | 0 / 0 | 0 |

All ten cases have zero older commit calls and identical before/after fingerprints for
all **36 tables mapped by ProcessPersistenceDbContext**. These are sorted canonical JSON
row fingerprints, including runtime, receipts, generations, funding allocations, journal
metadata and retained payload content; they are not merely row-count comparisons. Each
older result records loaded Application/Persistence/Runtime/test assembly hashes and
module IDs. Those hashes match the separately built historical outputs.

## Discovery, execution and negative evidence

Owning project: `tests/Integration/CanDoItAll.Tests.Integration` in both checkouts.
Candidate filter:
`FullyQualifiedName=CanDoItAll.Tests.Integration.ProcessRefreshJournalHistoricalProbe.Older_producer_retains_exact_current_state`.
Expected/discovered/executed: **10 / 10 / 10**, all passing, none skipped.
Historical filter:
`FullyQualifiedName=CanDoItAll.Tests.Integration.ProcessRefreshJournalHistoricalProbe.Read_saved_state_with_actual_older_producer`.
Expected/discovered: **1 / 1**; invoked once per scenario, all ten invocations passing.
These are ten distinct cross-binary cases, not twenty independent feature cases.

The initial execution failed all ten cases because plain System.Text.Json deserialization
of the temporary handoff defaulted the getter-only `RuntimeCommandId` wrapper to an empty
GUID. The older owner refused that invalid admission. The probe was corrected with typed
wire records and explicit validated ID construction; production serialization was not
changed. Candidate source/output identities and all pinned older production outputs are
identical across this test-only repair. The failed attempt remains retained separately.

Exact build/discovery commands, filters, TRX files, per-case child process records and
source/binary inventories are under:

- `.artifacts/concurrent-mutations/validation/c4-process-refresh-journal-compat`
- `.artifacts/concurrent-mutations/validation/c4-process-refresh-journal-compat-verified`

The successful historical build took 127.910 seconds, the candidate build 91.105 seconds,
and the historical test-only rebuild 45.235 seconds. The final ten-case execution took
205.561 seconds. It pins 1,296 current and 1,358 older source inputs plus 32 current and
11 older outputs. `qualification-summary.json` records the complete case matrix.

## Closure and limits

Both attempts' observers closed with zero remaining observed child processes, database
name deltas or temporary fixture path deltas. They reuse the existing isolated PG18 server
and bounded database leases; no replacement Docker stack was created. Both temporary probe
sources have been removed after hash verification, with reviewed source copies retained
in the proof directory. The normal Integration assembly was rebuilt in 62.419 seconds;
all 30 pinned production outputs and 1,295 non-probe source inputs remain unchanged.
The compiler server's exact PID/start time and parent match the recorded historical
build; it was stopped and its absence verified after the last build.

The complete final scan covers 10,156 files and 47,438 findings without truncation.
No baseline edit was needed: enforcement passes with all 15,703 reviewed executable-source
findings unchanged and without `--write-baseline`. Portability/secret/rendering tooling
tests, the rendering register, documentation and whitespace checks pass. No product C#,
project reference, migration or runtime configuration change remains in this checkpoint.

Both proof roots pass secret/sentinel review with zero unreviewed findings. Each has
three explicitly reviewed private-file/variable expressions in `Execute.ps1`; no private
credential value appears. Excluded/oversized text was checked separately for UTF-8 content,
hash and the private sentinel. The two bulk scan files were removed only after review and
hash verification; `portability-summary.json` retains their identities and complete scan
metadata. All 23 recorded build/discovery/execution/static/documentation commands have
exited with no cleanup, deadline or output-pump faults; historical command failures remain
distinguished from the passing final qualification.

Automatic approval review rejected the non-recursive removal of the verified temporary
junction `C:\repositories\jrc` with **blocked by policy**, before the command executed.
It points to the owned managed checkout
`C:\Users\lucys\.codex\worktrees\jrc\CanDoItAll`. The user authorized that exact retry.
Automatic review again rejected the verified non-recursive command before execution with
**blocked by policy**. Both resources remain preserved; the checkout has not been archived.
`alias-cleanup-blocked.json` and `alias-cleanup-authorized-retry.json` retain both rejections.

The exact historical producer can safely read/replay these five retained states in both
snapshot modes. This is not a mixed-version writer qualification, a schema downgrade
permission, or evidence for unknown-work resolution. The existing schema downgrade still
refuses retained journal rows. Actual delivery/cancellation handoff, explicit retry,
unknown-work policy, added-cost/capacity checks and public/native refresh/Replan remain
open. The previously recorded full Stable failures and wider engine E2E gates are not
reclassified by these tests. SPA remains untouched on `codex/concurrent-mutations`.

Earlier cleanup denials remain unchanged. No previously denied target was retried by
this checkpoint, and no whole-bundle cleanup claim is made.
