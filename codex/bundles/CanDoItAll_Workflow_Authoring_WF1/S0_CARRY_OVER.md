# S0 — bounded CA1 follow-up

## CA1-R1: prior setup evidence is relabeled after a later unknown attempt

Source: S02 (`CapabilityAuthoringSession.TestSetupAsync`), S03 (`CapabilitySetupPanel`), S04 and S29.
This is a source-derived finding. Establish its actual regression through the real renderer before
modifying behavior. It is not evidence of an authorization bypass, persisted corruption or a new
runtime dispatch error.

1. Open a valid MCP or Tool authoring session and complete setup A at revision R0. Return a distinctive
   safe diagnostic and, for MCP, a distinctive discovered tool name.
2. Edit to valid configuration B, revision R1. The old result is historical and hidden correctly.
3. Submit setup B and fault its current acknowledgement with a controlled exception after the operation
   has been admitted. Do not assume that cancellation or the exception proves no external effect.
4. Observe that catch sets `SetupUnknown=true`, `setupRevision=R1`, `setupSucceeded=null`, but does not
   reset `setupDiagnostics`/`setupTools`. Their getters now return A's evidence under R1, and the real
   panel renders it independently of the nullable success flag.

Fix ownership of result facts, not just the color of the status badge. Either retain complete immutable
attempt results with explicit historical origin, or clear stale current lists when a new attempt becomes
unknown. Keep any retained original result visibly attributed to A. Do not advertise a successful or
failed B diagnostic without evidence. A current unknown result must keep blind replay blocked.

Required controls: same-revision repeat; success A then different B; A→B→A; a real unsuccessful B result
with diagnostics; new edits while B waits; late completion after close/profile retirement; two distinct
editors; no extra setup request on render/Save/read-back; saved capability/proof facts unchanged. Check
both session properties and actual tool/diagnostic text in the form, not a mock status label alone.
A small local correction is sufficient; do not introduce a new runtime receipt journal.

## Delivery and historical qualifications

Verify the already completed Tooltip correction from the local sibling and its loaded binary/assets.
Do not redo the implementation or reset the sibling to remote just to produce a clean tree. Keep the
unpublished dependency explicit in final delivery metadata; it does not require stopping independent
implementation when the verified local source pair is available.

Keep CA1's mixed Stable and its focused follow-ups separate. The old PP3 timing failure remains
unexplained; the newer runner records numeric timing without changing the threshold. Preserve this.
Do not use the S0 phase to rerun the entire unchanged CA1 campaign or redesign History maintenance.
