# Sensitive state and owner safety

This file constrains Workspace Settings Core. It is not a new authentication design.

## Different origins must remain different

Workspace defaults and secret metadata belong to the selected canonical database profile.
The payload is resolved by the existing secret vault/protector owners. Provider history also
requires the current authenticated management context and expected policy version. Preferred
file applications are host-local control-plane data, not per-project or per-database settings.
Capture the correct origin for each session and retire only the state that origin invalidates.
A profile switch must never redirect an already admitted command into the successor database.
A file preference must not be relabelled or moved into another database after a profile switch.

## Secrets

Only an explicit selection in the active Secrets surface may load a decrypted edit value.
Header counts, lists, provider choices and background refresh use reference metadata only.
Keep reveal/copy as explicit existing interactions. Remask on leaving or replacing a revealed
surface; a retained input must not imply retained disclosure or a restart of its reveal timer.
Do not keep an invisible client-rendered decrypted subtree simply to preserve tabs.

Keep one short-lived private command capture for an admitted secret operation. Do not place
plaintext, encrypted payload, vault references, full command DTO or metadata JSON into generic
receipts, logging state, navigation, query parameters, clipboard diagnostics, JSON snapshots,
scenario URLs, error messages or artifact dumps. Metadata JSON can contain sensitive content too.
Persist/display only minimum nonsecret operation facts in history. Unknown recovery uses exact
identity and the current owner, never a replay of a stored plaintext command.

Keep an active unresolved editor available for correction/review when appropriate. On explicit
retirement, profile replacement and disposal, drop plaintext references and mask/unmount its
renderer. Do not claim cryptographic zeroization of immutable managed strings. Observe completion
of an admitted command without resurrecting the retired value into a new editor or tab.

A returned ID must be adopted before list refresh. A late success must not reset a newer draft.
Known metadata persistence followed by old-vault-payload cleanup or Activity failure is not a
rejected write. Distinguish durable metadata, payload availability/cleanup and diagnostics.
Do not infer commit from any exception or claim rollback merely because an acknowledgement is
missing. Do not remove secret-deletion reference policies, transaction coordination, locks,
protection, legacy payload handling or actual vault cleanup to simplify the UI.

Any narrow owner outcome addition must carry only redacted identity/stage/diagnostic code. Do
not turn this task into a distributed transaction or vault migration redesign. Preserve existing
non-UI callers and rebuild/test their actual semantics. If a pre-existing uncertain commit or
cleanup limitation cannot be changed safely in scope, report it explicitly and keep the affected
UI action unresolved, not replayable under a false success/refusal.

## Provider history

No policy/history read on initial tab open. Load is explicit. Preserve Manage authorization,
profile partition/write fences, expected version, bounded preview and the separate confirmation
for existing expiry changes. A new auth/profile/selection lifetime invalidates the preview.
No automatic shorter-retention update, worker execution, old prompt reconstruction or canonical
conversation deletion. Use owned test rows for destructive expiry proof, never actual history.

## File applications

Use the current owner's normalized extension, executable validation, host binding and durable
write. Expose unavailable/rebind-required states accurately. Saving an association does not
execute the path or a shell command. Do not launch an arbitrary user's installed application
as testing proof, or change their operating-system associations/control-plane file.
Keep path migration/rollback and provider-specific file launch outside the new UI command port.
Existing declared owner read/migration behavior is not replaced by a second implementation.

## Test isolation

Use task-owned PostgreSQL 18 databases, private ephemeral vault/control-plane directories,
synthetic secret sentinels and harmless executable fixtures. Test sentinels must be distinguishable
from real credentials. Redact sensitive test names/parameters at the evidence boundary without
hiding failures, skips or counts. Never capture the user's real Settings/Secrets page for proof.
Do not touch port 5032 or ordinary data, key rings, preferences, processes or shared Docker volumes.
