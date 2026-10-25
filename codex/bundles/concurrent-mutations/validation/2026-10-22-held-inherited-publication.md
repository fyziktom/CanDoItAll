# Held inherited initial publication qualification

Status: **Pass, Behavioral.** All 20 selected Integration cases pass on unchanged
production at `c7e0e8c61`. Automatic recovery activation and the final E2E gates remain open.

## Behavior

The new race uses actual Process enrollment, the registered inherited workspace producer,
File publication, PostgreSQL cancellation and the registered recovery stages. It holds
the exact child's run publication key after the original parent check. While the first
journal is missing, a full recovery cycle observes MissingJournal and retains the root,
child and allocation as pending. No provider request occurs.

Releasing publication allows the original measured preparation to become visible. Fresh
dispatch refuses the stopped parent. Recovery then cancels that journal, accounts exactly
the original one-second preparation charge, and closes the decision, scope, root and
queue. Both RunSnapshot and StepSnapshot pass. Cancellation advances the work revision
once while preserving all other work fields, including the original settlement.

The existing Core admission retains its dispatch lease across preparation/save. No new
production repair or publication contract was needed for inherited schema 7. Shared test
request construction was factored from the existing review fixture; the publication
listener now optionally waits for the selected original-parent boundary.

## Validation and retained failures

| Class | Cases | Result |
| --- | ---: | --- |
| ProcessInheritedPublicationCancellationRaceTests | 2 | Pass |
| ProcessCancellationCompletionIntegrationTests | 2 | Pass |
| ProjectTaskPlanProcessChildStopperTests | 14 | Pass |
| ProcessAdmissionCancellationRaceTests | 2 | Pass |

Exact discovery matches all current source methods. Integration builds in standard
Release with local libraries and serialized outputs. The 44 source pins and 32
owning/copied outputs remain unchanged after execution. Full portability includes the
new untracked test and passes without a baseline edit: **15674 findings unchanged**.
Tooling tests, rendering register, documentation and whitespace checks pass. Broad Stable
remains reserved for the final integrated candidate.

Two initial compile errors were confined to the test's namespace import and typed
allocation assertion. The first observer refused an ownership record missing the absolute
root equivalent to its already-recorded relative path; no test launched. Original
pre-creation evidence and the correction timestamp remain retained.

The first native run failed both cases because its listener selected an earlier catalog
wait, adding a second clock interval before initial save. The corrected listener waits
until the original parent check has completed. The second run reached the intended race
and completed recovery, but its full work-record equality assertion incorrectly omitted
the existing cancellation revision increment. The final assertion requires exactly that
increment and preserves every other field. Both failed runs, exact source and compiled
identities remain retained. Production stayed unchanged throughout these corrections.

## Architecture and resources

Architecture gate: **Pass with existing production follow-ups.** No production type,
member, partial owner or reference changes. All 30 production owning/copied output hashes
match the preceding signed registration checkpoint; its scoped architecture record remains
applicable. New coverage exercises the real producer without adding a runtime test seam.

The existing isolated PostgreSQL fixture is reused. All three native runs reconcile with
zero new processes, database names or temporary paths. The final observer covers all four
execution commands. Ordinary 5032 and earlier denied cleanup targets remain untouched.

[Compact proof](../proof/2026-10-22-held-inherited-publication.json): 147155 bytes, SHA256
`DD0F681F156D0C0921F5EA0AEB7259508F1C28535C77DAB74573D923DAA8F7B8`.
Local commands, TRX, test-only bridges and identities remain in
`.artifacts/concurrent-mutations/validation/c4-held-inherited-publication`.

Proceed to cancellation-aware owner deadlines and finite scheduling. This result does
not qualify OS process restart, noncooperative I/O bounds, whole-journal capacity or
worker/HTTP activation. Snapshot refresh/settings, remaining writers and final E2E still
need closure. SPA remains on `codex/concurrent-mutations`; main is unchanged.
