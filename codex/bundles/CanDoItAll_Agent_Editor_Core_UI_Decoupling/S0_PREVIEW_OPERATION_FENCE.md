# S0 / P2-R1 — stale preview failure in the same activation

## Evidence and exact distinction

`ProjectFilesSurfaceSession.ActivateAsync` calls `Begin(origin)`. That cancels the previous
operation and assigns the next one, but both requests may still share the same accepted
`ProjectFilesActivation` and `ProjectFilesOwnedWorkspace`. Success checks token/context. Its
non-cancellation catch publishes `ActivationError` when **IsCurrent(origin)**, without checking
whether **origin.Operation is still the admitted operation**. `Finish` correctly checks identity
before clearing the slot and must keep doing so. [R03, R04]

Existing unit controls cover a late preview across different activations and an old A open after
B/new A. They do not cover two preview requests in one unchanged activation. [R05]
The real Files view keeps FileBrowser visible until the preview succeeds. FileBrowser's dispatcher
checks the rendered item/snapshot at callback entry, then awaits the host callback; it does not
serialize outstanding host preview operations. A second recognized keyboard/file invocation can
therefore enter while the first waits. This is not a request to change the shared dispatcher. [R06, R07]

## Failing-first schedule

1. Open an owned workspace with two real visible items A and B; capture accepted activation.
2. Invoke A through the actual FileBrowser renderer. Hold its provider after admission; let it
   intentionally ignore cancellation, as an external operation can do.
3. Invoke B in the same browser/activation. B succeeds, publishes its own request/content/grant.
4. Complete A with an ordinary IOException or renderer-safe provider failure, not an OCE.
5. Observe that A's old error is currently displayed alongside B. No wrong bytes need be written
   for this to be a real presentation bug.

Variant: B fails with a distinct current error, then A fails; B's error must remain. A retained
old snapshot callback after a whole-activation change is a separate control, not this reproduction.

## Required repair contract

Publication must require the matching active operation AND activation/context, including exception
paths. Use the current idiom rather than inventing a universal async framework. A cancelled stale
operation may be diagnostic, but may not change the current error, current preview, busy state,
action feedback or another operation's completion. Do not silently hide errors of a current request.

Preserve original cancellation/cleanup ownership: request sources are disposed only by their
finishing requests, late acquired grants are released once, and an old finally must not release
or cancel a newer operation. Do not turn a presentation failure into proof of no external action.
Do not serialize unrelated surfaces or remove the ability to select B while A is noncooperative.

## Minimum controls and production follow-up

| Case | Required assertion |
|---|---|
| A pending → B succeeds → A fails | Same accepted activation; B's content identity/bytes survive; no A error published |
| A pending → B fails → A fails | B's exact current diagnostic survives |
| Current request fails | Error is visible, correct recovery path works |
| A→B→A within same activation | Earlier request cannot overwrite the newer same-public-ID operation |
| Preview superseded by an action | Old failure cannot annotate or finish a newer action; one explicit action dispatch |
| Stale success / owner close | Acquired old grant released once, new grant remains usable; existing cross-activation tests stay green |

Start with unit controls, then an actual host+renderer test with controlled owner/provider barriers.
Exercise both wrappers using the shared session; one honest production Files read/download journey
checks composition. A deterministic test may use private reflection only if established test hooks
make no public seam viable; do not add a production test-only toggle or replace FileBrowser markup.

This finding is source-derived, not reviewer runtime reproduction. Do not claim the bug has been
reproduced until the control actually fails on the unmodified source. If current checkout already
repairs it, retain the distinguishing test and explain why it passes. Finish S0 before A1.
