# Live planning owner and work accounting

The Core owner now binds the existing real dispatch lease, atomic file publisher and
journal clock to one live work handle. Basis capture runs inside its charged interval.
RunSnapshot reuses the exact retained material; StepSnapshot captures again. Provider
entry records a durable origin even without tools. Responses and publication bindings
commit together; approval waits and terminal settlement update the run and accounting
in the same publication. No new store, lock, partial class or project reference exists.

All **114 discovered cases pass** on the final candidate: 66 Unit and 48 Integration,
including 20 new file-owner cases. They cover initial-only admission, exact revisions,
foreign/disposed handles, both two-step basis modes, zero-gap accounting, both clocks,
positive monotonic rollback, budget exhaustion, invalid publication contracts, actual
execution cancellation, approval bindings, failed capture, and lost start/response/close
acknowledgements. A restarted owner cannot reset an unresolved interval's clock.

The initial candidate passed 111/112. Its rollback test exposed a production defect:
known-response recording preserved the prior work observation but moved the enclosing
run timestamp backward. The file owner correctly rejected that update. The repair keeps
the previous run timestamp only for explicit outcome recording. The response reports
ClockInvalid and creates no publication; settlement still refuses an invalid clock.
Two additional cases cover the actual cancellation merge, including failed decisions.

Both final Release builds, portability enforcement (15,610 reviewed findings, no delta),
rendering, documentation and diff checks pass. Snapshot
`snap-20261019045238-daf4d496` covers five projects, 526 documents and 1,937 types.
Project references are unchanged. Four existing type cycles remain; the additional
owner/handle cycle only performs reference-identity comparison and no reverse call.
The diagnostic set is 189 existing package advisories plus six diagram-size notices,
with no other compilation error. This is a scoped architecture pass for the live owner,
not activation of the product planning path.

Both candidates verified unchanged frozen source and runtime. Final resource checks
found zero remaining observed processes and zero database-name delta; the final run
preserved six unowned temporary paths. The isolated PostgreSQL fixture is retained for
the remaining authorized work. The separately recorded profiler cleanup block remains.

Proof: [initial candidate](../proof/2026-10-19-planning-authority/c4-live-planning-owner-validation.json)
and [passing repair](../proof/2026-10-19-planning-authority/c4-live-planning-owner-clock-repair.json).
The latter contains all 114 actual results; no result is inherited from the failed run.

Next: real MAF provider inputs, module-owned proposal preparation, v2 publication,
saved approval/Refresh/Replan actions and native/API/SPA proof. Process/Workflow policy
execution, maximum-size/sustained capacity and final resource cleanup remain open.
