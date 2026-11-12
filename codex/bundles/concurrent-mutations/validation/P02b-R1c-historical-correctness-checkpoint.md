# Historical receipt correctness checkpoint

Status: focused historical-read correctness, both actual-restart cases and the
Send browser case pass across the recorded repair runs. This is not overall
concurrent-mutation completion or a capacity qualification.

## Executed evidence

The first 133-case candidate executed 121 passed and 12 failed. Its repair preserved
original profile, generation, authority, payload identity and receipt semantics.
The repaired Unit 43 and Components 50 selections passed. Integration initially
passed 28 of 40: six PostgreSQL cases failed before lease allocation because the
parent used the wrong environment-variable name, and six no-write cases observed
delayed Windows directory notifications. The six unchanged PostgreSQL cases passed
with `CANDOITALL_TESTS_POSTGRES_CONNECTION` on the same source. The six unchanged
no-write cases then passed after a bounded observer-setup drain. Thus all 133 distinct
ordinary cases now have a passing result; this is a scoped repair union, not one
fresh 133-case execution or a broad Stable pass.

The observer keeps the same notification filters, overflow refusal and strict zero
nonsentinel-event predicate. Separate Windows controls reproduced directory events
without product writes, then verified that the setup drain still detects actual file,
directory timestamp and permission changes. The observation supplements original-byte
and no-coordination assertions; it does not promise exhaustive OS-event capture.

The actual interactive restart Fact passed after adding normal routing registration
to the parent and seed service containers. Dependency validation remains enabled.
Seed A exited naturally; standard Web B read the original accepted effects through
authenticated HTTP, including current read revocation and target-lifetime refusal.
Its owned children, listener, database and environment were independently reconciled.

The Process seed also exited successfully, but its first post-seed arrangement failed:
moving expiry back one minute put it before the original execution timestamp. The
successor expires the claim at captured current UTC, asserts that the original
execution remains inside its valid interval, and requests a dispatch-capable observation
so the expired-current-dispatch assertion is meaningful. Production checks are unchanged.
That successor reached the first HTTP read, then correctly failed the unchanged
Process-state hash assertion. Process dispatch and claim recovery have a separate
`Processes:RuntimeDispatchQueue:EnableRecovery` switch, enabled by default, outside
the generic background-worker lane condition. The next controlled observer explicitly
disables that existing option and asserts its effective registration; it preserves
the strict state/hash checks. Both actual-restart Facts then passed on that same final
configuration: interactive in 33 seconds and Process in 55 seconds. The latter reads
historical effects with an expired dispatch claim, stops Web B, reactivates through
the supported control plane and verifies DatasetChanged in fresh Web B2. All five
owned child identities, listeners, databases and environments were independently
reconciled absent. Both failed attempts and their verified cleanup are retained.
This observer does not qualify concurrent ordinary background recovery.

The optional Send browser Fact passed in the original run, whose parent missed live
ancestry capture. A separate successor passed with capture armed before launch:
13 retained process identities all exited, the exact logged sandbox matched its
retained PID/start ancestry, its listener was absent, temporary roots were unchanged,
and all 229 served runtime/static assets retained their bytes and path set. Observed
polling gaps reached 465.6127 ms; this proves the recorded ancestry, not arbitrary
unobserved short-lived processes. Draft editing remained available while unconfirmed
outcomes blocked Send.

## Source and validation boundaries

The observer/routing candidate froze 8,724 Git source paths at manifest
`BEB64576513C730504F435D969B47346B3190930396CECC4D85CC817CC708387`
and 4,182 restart runtime files. Final hash/path-set/HEAD verification passed before
the expiry-fixture successor was transferred. Its Integration Release build also
rebuilt the referenced seed host using standard outputs.

The final restart candidate froze 8,729 Git source paths at manifest
`1D694A674B210B08AD9CA9553B11D15E9A1098D34D32A551AAC5B17F5DC43079`
and 4,182 runtime files at manifest
`660E875807D50A94638119B9FFCB2489D463ADA15412C93A75DD0B612C49DFCC`.
Both discoveries selected exactly one Fact; both executions and final source/runtime
hash, path-set and HEAD verification passed before release at 05:30:42 UTC.

The complete final scan for that candidate contained 36,844 findings across 8,655
scanned files, with no truncation. All 13 added findings and zero stale findings were
reviewed: noncompiled predecessor/prose/inventory evidence and the bounded fixture
enumeration/link refusal. Protected executable findings remained exactly 15,500;
final enforcement passed without writing the baseline. Rendering registration passed
for 1,201 inputs and documentation for 427 maintained files. The fresh final restart
scan contains 36,849 findings over 8,660 scanned files without truncation. Its five
added findings and zero stale findings are reviewed inventory paths and explanatory
prose; protected executable findings remain 15,500 and no-write enforcement passes.
Rendering and documentation checks again pass at 1,201 and 427 respectively.

Raw commands, discovery, TRX, manifests, failures, process observations and cleanup
remain in the owned ignored validation directories: `r1c-historical-candidate-validation`,
`r1c-historical-repair-validation`, `r1c-historical-process-env-retry`,
`r1c-historical-observer-drain-validation`, `r1c-historical-restart-path-cache-validation`,
`r1c-historical-restart-routing-validation`, `r1c-historical-restart-expiry-validation`,
`r1c-historical-restart-observer-validation`
and `r1c-send-browser-ancestry-validation`. Earlier failed attempts are not relabelled.

## Remaining implementation gates

Task-plan persistence needs the reviewed receipt-uncertainty correction, canonical
169-entity migration and PostgreSQL proof before producer activation. Resource owner
integration must resolve same-project lock contention and validate deletion's snapshot
freshness. C4 persisted decision policy still requires real provider/approval/owner
integration, complete writer exclusion, work accounting and settings cutover.
Whole-journal publication capacity, D1/client compatibility, background Process/Workflow
writers, other owners, broader Stable failures and final resource cleanup remain open.

Incremental commits already reach `702d0a93`; the separately staged SPA transport commit
is waiting on the existing GPG signing prompt. Signing has not been bypassed and a
second blocked commit has not been started.
