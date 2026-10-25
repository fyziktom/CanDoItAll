# P28: pure capture invocation journal

Status: Pass. All 35 selected Unit cases, final static enforcement, source/output
verification, secret review and owned resource cleanup pass.
Behavioral proof tier. This qualifies the Runtime protocol only. Persisted reservation,
invocation admission, returned-result storage and registered producer integration remain
the next checkpoint under the [recovery design](../plan/P28-process-refresh-recovery.md).

## Owned behavior and limits

The user requires safe concurrent user/agent/process work, clear module boundaries and
performance risks resolved before implementation. The pure Runtime owner now distinguishes
a reservation that has never admitted invocation, an admitted invocation with unknown work,
an immutable returned outcome and a completed settlement. Its reservation retains the
original run, admission, reservation command, budget allocation/grant, scope, content
contract, material reference and accepted-input fingerprint. Grants remain bounded to ten
seconds and consume no automatic replan.

Only a changed Begin result represents the first admitted invocation. Identical Begin
replay returns `Changed=false`, including after return/settlement; it is never permission
to invoke again. A changed invocation token conflicts. Cancellation can close Reserved
as NeverInvoked with zero work. Once invocation is admitted, the same cancellation cannot
establish zero work or refund the grant. No lease age, clock or retry counter can resolve
that unknown state.

Captured outcomes bind their original content material and actual closed work. Root/step
capture retains the returned payload; local shared-material inheritance retains only its
stamp and original capture time. Failure outcomes retain exact work without a publishable
payload. Overrun evidence is preserved rather than clamped. An exact outcome replays;
changed content, capture time, work or outcome kind conflicts. Settlement must match the
same budget, allocation, grant and returned work. Successful advance additionally requires
captured material and no accounting overrun. A completed disposition cannot be replaced.

The shallow-pass trap is treating any prior reservation as capture permission or any
interrupted invocation as zero-work cancellation. The negative tests reject both, plus
changed replay evidence. Positive tests run root capture, step capture and shared inheritance
through actual `DecisionBudgetTransitions.Reserve/Settle`, proving one charge and grant
release. These are pure domain tests, not SQL-race or host-restart proof. No schema, queue,
DI registration, provider callback, public API or automatic retry was added. The existing
producer does not yet consume this journal. There is no production stub or simulated
persistence path standing in for that missing integration.

## Selection and execution

Entry prerequisite: signed producer `3f5415f623bd5a27ccca1ad7cda14856c431e2b4`.
Runtime and its owning Unit assembly were rebuilt in standard Release with local libraries.
The first builds failed on a missing Drivers.Abstractions import; the corrected direct
Runtime build passed in 2.446 seconds and Unit build in 59.992 seconds. Existing package
audit/analyzer warnings remain; there are zero build errors on the corrected candidate.

The exact filter is
`FullyQualifiedName~CanDoItAll.Tests.Unit.ProcessDecisionRefreshCaptureJournalTests.`
in `tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`.
Expected and discovered: **35**. Result: **35 passed, 0 failed, 0 skipped**.
The command completed in 4.991 seconds (reported test duration 81 ms).

Build commands use `dotnet build <project> --configuration Release --no-restore
-p:UseLocalCanDoItAllLibraries=true /m:1 /nodeReuse:false -v:minimal`.
Discovery/execution use `dotnet test <test-project> --configuration Release --no-build
--no-restore -p:UseLocalCanDoItAllLibraries=true /m:1 --filter <filter>` with `--list-tests`
for discovery and TRX logging for execution. Exact argument arrays and results are retained
under `.artifacts/concurrent-mutations/validation/c4-process-refresh-capture-journal`.

Runtime journal/model/test changes invalidate this selection. A later persistent consumer
must add real transaction/race/producer/migration proof. This as-yet-unconsumed protocol does
not invalidate unrelated packages or require an unfiltered suite. The actual budget
transitions are the current dependent consumer check; no prior producer test is relabeled
as journal integration evidence.

## Architecture and closure

CodeAnalytics `snap-20261023211340-17b44d93` covers Application, Persistence and Runtime:
319 documents, 945 types, 7,771 members, three unchanged scoped project edges, zero cycles.
The 189 existing WS0002 package-audit collector diagnostics remain. Direct builds supply
compilation evidence. Findings increase from 241 to 242: the reservation's 14 source
members form one immutable original-work descriptor; no partial splitting is warranted.
The Runtime protocol uses existing Abstractions/Drivers.Abstractions/SharedKernel references
and no infrastructure or Application dependency. No project files changed.

Source/output verification pins 1,282 source inputs and eight relevant Release outputs,
including Runtime/SharedKernel copies in the Unit output. Verification passed before and
after the tests. The pure lane creates no application/database fixture. Reconciliation found
the compiler server started by the first Runtime build (PID 40360, verified parent/start
time); it was stopped after its last use, and the fourteen command records have no remaining
owned process matches. Both reviewed bulk scan files were removed separately after exact
path/hash checks. The standard outputs and small reviewed proof are retained for subsequent work.

Full portability scanning included both untracked C# files: 10,141 scanned files and 47,427
findings, no truncation. Neither new source file produced a finding. Final enforcement
passed with all 15,697 executable-source allowances unchanged; no baseline write occurred.
The 6 portability-tool, 4 secret-tool and 7 rendering-tool tests, rendering register,
documentation and whitespace checks pass. Secret review covers 54 scanned text artifacts
plus all four excluded/oversized UTF-8 files, with zero findings or credential-sentinel
matches. Compact manifests preserve scan hashes, coverage and cleanup evidence.

The recovery design now requires bounded journal metadata transactions under the exact
existing root advisory fence. The full runtime UOW loads and replaces history collections;
two extra timestamp-only commits through it would amplify history-dependent cost. Keep
reservation and final budget/scope/successor settlement in that UOW, and prove the narrow
metadata writer serializes with it before activation. This design finding is not a measured
throughput qualification.

The remaining durable capture journal, unknown-work resolution, explicit retry, delivery,
public refresh/replan, remaining engine owners and full E2E stay open. SPA remains clean
at signed `11399398e4b836116a6a9b06291f2619368fd4db` on `codex/concurrent-mutations`.
Historical cleanup denials remain recorded; this checkpoint does not retry their targets.
