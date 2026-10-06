# S0 — WB5-P1: quick-create belongs to its original participant editor

## Source finding, not a reproduced incident

`ProjectStructurePage.PartyIntegration.cs`, method `CreateParticipantPartyAsync`,
passes the mutable `partyEditor.QuickCreate` to the bridge. After the native create
returns it reads options using current `ProjectId`, changes current `partyEditor`,
replaces its QuickCreate and clears the page-wide busy flag in finally. It does not
capture a distinct editor/operation receiver or reject an already admitted duplicate
inside the handler. [S14, S15]

A previous read path has project/admission/selection fencing, but that is not the
lifetime of this mutation. The file is unchanged by WB4. Do not call this a newly
introduced WB4 regression, an observed data incident, or an authorization bypass.

## Deterministic first reproduction

Use the actual routed participant editor and native directory/assignment stores, with
a controlled barrier at create completion or the subsequent native option read.
Do not fabricate an entirely fake successful directory record.

1. Create owned project P, participants A and B, and an untouched neighboring record.
2. Open A, enter a valid distinct quick-create draft and dispatch the actual button.
3. Hold its owner operation. Select B and allow B's editor to settle. Give B distinct
   draft data; optionally admit its own independent operation.
4. Complete A. Inspect both native identity and actual rendered B: A must not select
   its created party in B, clear B's fields or busy state, or publish an unqualified
   success for B. Exactly one directory create must correspond to A.
5. Repeat with A's post-create list read failing. Accepted PartyId must remain known;
   read retry must not call CreateParty again.

Also exercise A→B→A, project recreation with the same public IDs, profile/actor change,
close during accepted work, same-selection refresh while typing, a direct second
handler invocation/Enter, and two independent page instances. A test that only changes
to a project with no valid matching node can mask the original-target defect.

## Required correction

Capture one immutable request, original project admission/node occurrence, selected
editor instance, actor/profile context and operation identity before dispatch. Admission
must exist in the handler, not only Disabled markup. Retired work may finish at its
original native owner; it cannot acquire a successor's authority or clear its admission.

Retain the concrete accepted PartyId before any read-back, selection publication,
notification or metadata operation. Creating a directory record is not saving the
participant's canonical assignment. If the accepted record cannot be reloaded, show
the accepted identity and an explicit read-only recovery path for that same record.
Do not auto-delete the created directory object as compensation or discover it by name.

Trace the real `CreatePartyAsync` implementation and its called owner before patching.
The review did not establish every underlying commit stage. Reuse current native
receipt/result semantics; add a narrow result only if the existing owner genuinely
cannot return its accepted identity. Do not change directory-wide identity, transactions
or authority as an incidental UI redesign.

Record the failing attempt, smallest fix and fresh positive/negative evidence. Then
continue W1-W4; stopping after this fix does not complete WB5.
