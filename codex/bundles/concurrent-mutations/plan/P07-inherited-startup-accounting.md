# Inherited Agent startup ownership and accounting

Status: accepted for implementation, 2026-10-20. Selected Process production has 187 distinct passing behaviors. This continues the original end-to-end work; actual executor activation remains gated.

The current admission owner correctly serializes creation by exact execution ID and
recovers the first durable journal. It does not know whether the caller freshly
enrolled the parent allocation, and preparation before that save is uncharged.
The workspace recovery path also prepares its Agent/provider before opening work.
These are concrete gaps to close before routing the Process producer into Core.

Use an explicit start contract containing the original finite allocation and a
typed mode (fresh parent enrollment or existing execution only). Require it in
the workspace request and exact admission entry; do not keep a permissive default.
The parent owner supplies fresh mode only for CommittedNow enrollment. An existing
execution is always resolved first. Inside the exact child's dispatch lease,
resolve it again and reject a missing journal in recovery mode before any
preparation or binding callback. The eventual bound origin must match the
supplied original allocation, including grant and identity.

Measure new preparation, binding and parent verification using Core's TimeProvider.
Apply a linked finite deadline bounded by the original grant and the explicit
initial preparation limit. Account successfully closed preparation in the first
durable child journal, using a new append-only InitialAdmission settlement reason.
The existing work record can hold closed ticks with no provider steps. Add one
strict initial-only transition; it cannot charge an existing decision twice,
charge interactive work, replace its origin or open provider work.

Measure monotonically, reject a backwards clock and retain the larger measured
elapsed interval where the existing work policy requires it. Persist actual
overrun as failed closed work and prohibit provider entry. The accounting save
is outside the measured operation, as with existing parent capture settlement.
If preparation fails before a valid durable child exists, do not manufacture a
zero-work outcome. Its parent allocation remains unresolved for reconciliation.
Lost acknowledgement after the save recovers the original measured journal.

Move recovered provider startup behind StartInheritedAsync. Keep the public
already-prepared coordinator entry, but share one lifecycle implementation with
a deferred startup callback used by the workspace. That callback must execute
under the original child's bound live lease and finite remaining-work token.
Validate the admitted runtime contract before entering the provider loop.
When the owned startup callback has closed without entering the provider,
settle its measured work as failed with the existing terminal owner transition.
An unknown opening/settlement acknowledgement retains its original state for
receipt reconciliation; it must not restart from an empty journal.
This intentionally changes the old recovery test that asserted no journal
change despite performing unaccounted startup.

No Process/Workbench/EF dependency enters Core. No new runtime partial, provider
implementation, service locator or trivial interface is needed. The existing
admission, coordinator and workspace orchestration own these changes.

Required proof: fresh versus retained entry, two callers and independent children,
initial preparation and binding elapsed work, known overrun, cancellation,
negative/backwards clock, mismatch of original allocation, lost acknowledgement
at every initial save stage, missing journal after unknown startup, recovery
startup under an open interval, changed contract and exact original input.
Repeat real registered MAF workspace tests, inherited journal persistence and
ordinary planning regression selections. Keep source frozen across builds and
tests and retain the initial selected-producer failure separately.

Next product work remains actual Workbench initial binding, shared ordinary
Process preflight, exact manager approval/cancellation/refresh with historical
source access, typed waiting-step completion, allocation-capacity qualification,
Workflow occurrence integration, persisted settings and full writer closure.

The mandatory start contract carries the original allocation and a typed FreshEnrollment or ExistingOnly mode. Initial preparation is capped at the lesser of ten seconds and its original work grant. The initial settlement reason is appended without renumbering existing persisted values. Existing no-admission-work journals remain readable. Recovered local startup failures are settled only after the owned callback has returned and before provider entry; unknown commit responses remain reconcilable.

Initial execution completed all 189 cases with 171 passes and 18 failures. Four
cases reached their expected production outcome before an invalid default-array
assertion; fourteen hit a duplicate participant registration in the workspace
fixture. The bounded repair changes three test files and repeats all 44 cases in
the four affected classes. Preserve the initial proof and require exact unchanged
production source/binaries before carrying forward the other 145 passing cases.
See the [startup validation record](../validation/2026-10-20-inherited-startup-accounting.md).

All 44 repaired cases pass with 666 unchanged production outputs; together with
145 unchanged regression passes, the accepted startup scope has 189 distinct
passing behaviors. Exact identities and resource reconciliation are retained.

Additional boundary design accepted on 2026-10-20: the remaining-time calculation
currently occurs as a timer constructor argument outside the owned startup catch.
If the grant expires after the interval is durably opened, that known local
failure must settle instead of leaving unexplained open work. Construct the timer
disabled with the original TimeProvider, then set its finite delay inside the
existing catch boundary. Check cancellation before invoking preparation. Preserve
the distinction between closed local startup failure and unknown provider/lifecycle
failure. The .NET 10 CancellationTokenSource implementation retains its supplied
TimeProvider timer when CancelAfter changes an initially infinite delay
([runtime implementation](https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Threading/CancellationTokenSource.cs)).

Prove exhaustion and cancellation at the exact bound-interval boundary with a
forwarding test clock that advances only once after the live lease is bound.
Neither preparation, completion nor provider work may enter; measured work must
close with the correct failed/cancelled outcome. Repeat the affected coordinator,
lifecycle, workspace and registered MAF cases. No new production abstraction,
dependency, runtime partial, policy default or timer-duration fallback is required.
