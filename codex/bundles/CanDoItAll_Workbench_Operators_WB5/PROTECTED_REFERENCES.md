# W2 — protected secret reference and creation forms

## Complete presentation, unchanged vault authority

Move the actual current reference picker, search, saved/missing selection states, purpose,
reference note, edit mode and Create new secret fields. Use the real SecretField/CopyButton
and overlay primitives. The normal picker reads metadata only; do not fetch a stored secret
value to build previews, render a dropdown, resolve a missing option or make sandbox data.
A brand-new value typed by the operator is a transient sensitive draft, not persisted
navigation, evidence, a retry description or a globally shared session. [S16]

Retain original project admission, node/parent occurrence, reference-edit opening, actor and
profile. Reopening the same ID is a new interaction. Changing the selected secret or search
filter must not rebind a previously submitted operation.

## Creation has multiple phases

The current path copies a SecretEditorModel, calls the real SecretService, reloads the
metadata entry by returned ID and then creates/updates the project reference. Model these
actual boundaries explicitly. [S16]

| Owner fact | Required behavior |
|---|---|
| Refused before vault commit | Preserve correct draft for correction; distinguish access/validation/conflict |
| Secret created, metadata read fails | Keep its exact ID and allow only original metadata observation; never recreate by name |
| Secret created, reference write fails/refuses | Report the accepted vault object separately; no claim that nothing was saved |
| Reference created/updated, surface read fails | Retain node/secret IDs and retry reads without another write |
| Outcome genuinely unknown | Do not resubmit blindly or discover a match by first duplicate name |

Inspect existing Workspace/Secrets UI owner-result contracts before adding new ones. Prefer
reuse of native phases and a narrow adapter/result over a second vault abstraction or general
operation registry. Creation of a vault object does not implicitly grant an Agent access to it.

On confirmed creation, retire unnecessary copies of the entered value while keeping safe ID
and phase metadata for observation. On close/profile/auth replacement, remove only the owning
sensitive view. Do not claim secure erasure of immutable strings; minimize retention and
ensure no copy appears in rendered descendants, persistent view state or logs.

## Mutation and callback discipline

The current disabled button is not an admission gate. Use one handler gate for Save, Enter,
Create-and-use and alternate submission routes within each opening. Two independent openings
must not clear each other's busy/error/selection. Snapshot callbacks and targets before awaits;
late metadata, secret creation, reference writes and disposal cannot close a successor dialog.
Native policy and expected-node checks apply at the writer, not just before it in the renderer.

Preserve the exact stored reference schema, SecretId/name snapshot semantics and existing
metadata serializer. Do not overwrite unrelated node metadata from a stale full draft.
No plaintext existing-value read is authorized by this extraction.

## Tests

Use only owned synthetic credentials, privately supplied and never included in screenshots,
TRX attachments or committed data. Prove metadata-only reads, reference to an existing secret,
create-and-use exactly once, two dialogs, name collision, cancelled read, missing/inaccessible
secret, actor/profile change and original-project recreation. Include fault barriers at the
actual vault commit and reference commit, with exact IDs and no duplicate retry.

Negative Agent/tool proof: an Agent without the relevant secret grant must still be refused;
only the intended explicitly granted test Agent may use its allowed reference. No automatic
AllowAll or plaintext prompt embedding is permitted. Re-run affected existing Workspace/Secrets
consumers when their shared owner changes.
