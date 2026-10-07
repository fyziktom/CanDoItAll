# WCL-R1 — exact profile admission at the real catalog write

## Evidence and scope

R09 performs an exact editor read in RequireMutableAsync before calling the facade's legacy
SaveProfileAsync. R10 then independently acquires the catalog lock/file coordination, looks
up the optional model ID, and builds/upserts a profile even if that ID no longer exists.
BuildPersistedProfile uses `existing?.Id ?? model.Id ?? Guid.NewGuid()`.

R11 proves deletion before the preflight, not after it. A single Blazor event need not yield
at every await for this race to exist: another circuit/thread/process can interleave between
two separately coordinated catalog operations. The defect is the missing exact write
precondition, not a claim that every asynchronous Save reproduces it.

This review has not run the reproducer. Confirm it before changing source. Treat it as an
exact-editor contract defect bridging old and new code, not established historical causality.

## Deterministic failing-first test

Use the real control-plane catalog in a private root and an actual WorkspaceDataSourcesOwner.
Create canonical A and inactive B through their real owners. Give B a real synthetic protected
password and record its ID without disclosing plaintext. Acquire B for edit.

Pause the original edit after its successful preflight/exact read but before the next catalog
write acquisition. The barrier belongs to a bounded test seam/decorator or test-owned
coordination adapter; do not add a production sleep or hold the global catalog lock across
an external wait. On a second independently scoped owner remove B, then release the original
Save. Verify whether B is recreated under its original ID and Save reports Confirmed.

The corrected result must be a known missing/stale editor refusal with no recreated B, no
new profile identity, no new active/pending pointer, no changed A and no physical database
or file deletion. The original non-sensitive draft survives for explicit user correction.
A previously supplied password must not be exposed or reclassified as metadata evidence.

Add an analogous acquisition-time test for a protected current/pending profile when possible.
Do not assume a read outside the coordination lock can enforce a later write. If enforcing
an additional rule requires a new cross-owner activation protocol, document that separately;
the missing-record race itself is bounded and should be repaired here.

## Implementation constraints

Prefer a narrow editor-specific entry point at the existing control-plane owner. Inside the
same existing coordination interval that reads and writes the catalog, distinguish deliberate
creation from editing an existing exact ID and reject missing/protected records before the
write. Reuse legacy implementation internally where possible; do not copy its serialization,
secret protection or host-path rules into Workspace.

Foundation must remain independent of Workspace.DataSources.Contracts. Use a neutral owner
result or exception already owned at that boundary, mapped in the Workspace adapter. Preserve
legacy Save/upsert behavior for known non-UI callers unless an inventoried, justified change
is required. Avoid adding a generic transaction library or a schema/versioning system.

The current canonical profile is not whatever future profile is stored for restart. Preserve
both identities. Do not clear a different process's pending selection because an older UI
request saw another snapshot. Do not promote UI-provided fields into authorization evidence.

The catalog file, active-selection file, physical database creation and bootstrap are distinct
acknowledgement boundaries. Keep known IDs and completed stages if a later stage fails. A new
postcommit finding is not fixed by calling the whole operation Refused or retrying Save.
Observation of a similar profile must not fabricate causality for an unknown create result.

## Required controls

| Case | Required behavior |
|---|---|
| Explicit New | Exactly one normal new ID, password protection and existing startup policy |
| Edit B still present | Exact ID preserved, same saved password when input blank, intended fields changed |
| B missing before acquisition | Existing known-missing refusal remains |
| B deleted after preflight | New race is refused at write boundary, no resurrection |
| Other C edited/deleted concurrently | No excessive global refusal of unrelated valid work |
| Current A, pending B, startup override | Existing protection retained and checked at the proper admission boundary |
| Same editor and late response | Keep EditContext and later raw input; do not redirect it to a successor target |
| Read-back or log failure | Preserve acknowledged owner facts; read retry is not a write retry |
| Two-host/private process restart | Running canonical A remains A; saved B applies only after owned restart |
| Delete profile configuration | Physical DB and managed files remain intact |
| Secrets/evidence | No plaintext, ciphertext, connection strings or raw exception payload in receipts |

Run the real DataSourcesOwnerTests, control-plane persistence/canonicality owners, transfer
consumers and actual Data Sources browser journey. Do not use an in-memory dictionary as the
only proof of the coordinated write. Retain a narrow regression for every changed invariant.
