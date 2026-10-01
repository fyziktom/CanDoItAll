# State, form and compatibility requirements

## One authoritative editor

Retain the host's AgentEditorSession target, mutable draft and EditContext. New renderers receive
only the data and actions they need. A projection is not a second persistent agent and the original
full request must not be reconstructed from four tabs. Neutral supplied EditContext/model contracts
are acceptable after graph/exposure review; calling implementation owners through the model is not.

Typed intents must bind the rendered editor lifetime and relevant field/target. A create starts a
new draft; a section change does not. Preserve current catalog-selection semantics when Clear is
used. Preserve explicit reset/close behavior rather than silently autosaving. New read responses
must not recreate a valid form or discard incomplete raw numeric/date/text input.

UI input must be recorded before blur where the real shared widget supports it. A held operation
must not turn a later edit into an older snapshot. Changes away-and-back are still edits when the
field's intent matters. All four selected sections and deferred content share one form/validation
boundary; do not nest EditForms or bypass validation with an alternate footer button.

## Save and read-back

Capture exact source target/version/provider intent and the complete submitted request before the
first external await. Preserve one write gate across all actions using the same actual command.
Known rejected writes leave the original draft editable as the existing contract permits. A known
commit binds its actual identity before read-back; a post-commit warning is not a rejected write.
Read-back retry never reissues Save/Verify/generation. A genuinely unknown write remains protected
from blind replay. If an effect is accepted before the view retires, cancellation is not evidence
that its durable side effect did not happen.

The current host uses `AgentEditorSubmission.HasLaterEdits` and only updates the version when a
newer draft exists; otherwise it adopts the read model. Keep that behavior or justify a smaller
more precise merge with tests. Do not import Projects' per-field reconciler as a mandatory pattern.
Do not let a late read adopt another owner's version and then overwrite that owner's changed
permissions without an intentional, correctly tested conflict policy.

## Hidden/deferred-data round-trip inventory

A production test should seed a nontrivial original agent through the ordinary owner, load it in
the real editor, change only selected core fields, save, then read through the ordinary owner.
Verify at least the following retain their exact meaning (normalization may be owner-defined):

| Group | Values to protect |
|---|---|
| Identity and metadata | ID, expected version, managed/template flags, TemplateKey, Favorite marker, user tags and unknown ConfigurationJson extensions |
| Runtime fields not necessarily rendered | Temperature, background responses, per-service history requirement and other preserved configuration |
| Permissions | Observation/scheduling flags, tool-use and approval flags; no default AllowAll or automatic approval caused by mapping |
| Project access | Read/write subdivisions, create/subproject flags, allowed IDs and captured lifetime bindings |
| Workspace/Storage | File/script/environment flags, selected profile, external aliases AND host bindings, storage IDs and read/write flags |
| Secrets | Reference IDs/purposes only, no secret value resolution to render or inspect a reference |
| Process access | Stored allowed definitions and flags, even though the picker is currently unavailable |
| Memory | Invocation/tool/ingestion flags, provider assignments/bindings, allowed/denied capabilities and source scopes |
| Capabilities | Assigned IDs and verification facts; existing immediate-save behavior for an existing agent is not changed by A1 |
| Images/Voice | Preferred provider and model/default semantics, asset flag, configured voice/inherit meaning |

This is an extension of existing native round-trip evidence, not a copy of private production data.
Use distinct sentinels for safe IDs/settings. Do not store bearer tokens or plaintext secrets in
this inventory. Original source basis: R14, R15, R20.

## Deferred mount and action lifetimes

The new shell must keep the existing ten section order/token mapping. Production delegates the
six deferred sections to their real hosts. Preserve necessary keep-alive behavior where raw fields
would be lost by unmounting, but do not eagerly run reference/backend work just because a section
label exists. The present tests require no project list read until explicitly requested; a Memory
panel's current read count must not explode across section toggles. [R19]

Confirmations (auto-approval, delete, workspace risk) belong to the original session/target.
Only the core Runtime approval control is moved here; deferred Workspace confirmations remain
host-owned. Cancelling a confirmation must not mutate persisted state; confirming an old dialog
must not affect a newly reset editor. Keep ordinary operator confirmation semantics and managed
agent deletion refusal at the backend. Do not treat hiding a button as enforcement.

## Errors and exposure

Keep reference failure independent from core draft acquisition. No provider fallback chosen solely
because loading failed, no permission list cleared to represent an unavailable read, no fake empty
catalog hiding failure. Metadata/model diagnostics should remain actionable and safe. The existing
adversarial tests inject private sentinels; extend those through the new renderer without copying
their literal sentinel strings into shareable execution/discovery artifacts. [R21]
