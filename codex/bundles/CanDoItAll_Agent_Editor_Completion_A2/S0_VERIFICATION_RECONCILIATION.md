# S0 — A2-R1: preserve edits that existed before capability verification

**Source-derived functional defect; not executed by the reviewer.** It predates the A1 lift:
verification and save reconciliation retain the earlier common control flow. It is a loss of
uncommitted editor work, not proven durable data corruption or authority bypass.

## Exact causal path

R04/R10: `VerifyCapabilityAsync` captures `AgentEditorDraftPolicy.Capture(owner.Draft, ...)`,
calls `EditorCommands.VerifyCapabilityAsync(agentId, capabilityId, token)`, then records a
`CapabilityVerification` pending refresh and invokes `ReconcileSaveAsync`.
`ApplyReconciledEditor` checks whether the live draft differs from that capture. If not, it
calls `owner.Load(refreshed.Draft)` and replaces the draft/EditContext with canonical stored data.
R11: `HasLaterEdits` compares the entire snapshot captured at dispatch, not the last saved baseline.

Thus an edit made BEFORE Verify is part of the submission snapshot but never submitted as an
agent configuration write. A normal successful verification read-back replaces it with the
persisted value. No noncooperating await or second concurrent actor is needed for the basic case.

R12/R13: native verification receives IDs, loads the persisted agent/capability/provider snapshot,
runs a diagnostic and publishes proof under exact-input checks. It updates capability evidence
AND advances the agent's `UpdatedAtUtc`. It cannot know an unsaved name in a Razor draft.
Its own mutation/version/receipt semantics are authoritative and must not be weakened.

## Failing-first proof

Use a task-owned host/catalog and a deterministic safe diagnostic, not real paid model inference.
Create a persisted agent, attach a known fixture capability, and acquire its actual editor.
Type a new Unicode name/instructions, a tag and a harmless access-reference change. Keep the same
EditContext and a validation marker. Through the real capability list invoke Verify, with no more
edits after the click. Verify publishes once; canonical configuration fields remain old; all local
unsaved data, validation and selection must remain. The current code is expected to fail this.
Also run the control with a clean draft so proof/version is visibly updated as expected.

## Repair contract

Separate diagnostic publication/read-back from saving a submitted configuration. Keep the original
editor instance/context on successful verification, along with edits made before and during it.
Refresh only relevant proof/catalog presentation and the exact version effects owned by that
publication. Use the existing proof outcome/receipt concepts (including `CapabilityProofReceipt`
and `CapabilityVerificationDisposition`) rather than inventing a parallel verification system.
The existing `IAgentCapabilityCommands` is a reuse candidate; do not blindly inject another large
manager or change the native diagnostic to save the draft.

A competing external config update must not be hidden by assigning whichever version the latest
read happens to return. Establish an attributable baseline/version using actual owner evidence;
if the result cannot be safely reconciled, retain a conflict/review state with the draft recoverable.
Do not infer the new version from local wall-clock time. A narrowly justified owner/adapter result
extension is allowed, with caller tests; a new durable protocol or schema is not.

Known rejected/superseded/pre-publication outcomes stay distinguishable from genuinely unconfirmed
publication. Retrying read-back must never rerun diagnostics. Explicit repeat diagnostics are a
separate operator intent. Preserve safe exact IDs/attempt/stage facts without retaining private
provider configuration or a serialized full agent in general UI history.

## Required variants

1. Unsaved changes before Verify; no later edits. Preserve fields, context, raw validation and tab.
2. More edits while diagnostic and read-back are each held; both sets survive.
3. Verification commits, read-back fails, explicit Retry succeeds: one diagnostic/publication only.
4. Two editors of the same agent: one receives proof; the other remains independent and native
   optimistic conflict is not bypassed.
5. A separate canonical configuration change between proof and read-back: no silent version rebase.
6. Reset/close/A→B→A during diagnostic/read-back; no stale callback, busy clear or success toast.
7. Native typed refusal, superseded diagnostic and genuinely unknown publication; inspect exact
   published state and preserve no-replay protections.
8. Following explicit Save persists retained local edits once with a valid attributable version.
9. An unattached/removed capability remains rejected by the native owner; missing UI metadata does
   not make it assigned. Do not alter native policy to create a successful fixture.

Use current discovery counts. Assert actual owner state and exact proof/version, not only a toast.
Keep the original failed run and repaired controls. Then complete A2 rather than stop at S0.
