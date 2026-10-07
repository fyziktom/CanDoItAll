# State, native effects and recovery

## Independent ownership

Keep catalog acquisition, selected provider, source-list acquisition, edited source, discovery
selection, imported alias, confirmation and parent delivery as distinct state. They may share a
cohesive session but not one undifferentiated generation or busy flag that cancels unrelated
writes. Types/intents carry exact IDs and the origin that showed the action, never only list index.
A selection generation is not a durable concurrency token or proof that a write did not commit.

Drafts and EditContext survive section changes and harmless same-target refresh. Capture submitted
values before the first await. Accept only native returned identities/tokens. Merge accepted fields
that were not edited later; keep raw invalid text/validation and row identity. Remote-owned fields
have a coherent snapshot rule distinct from local writable fields (S0-R2). Clear missing references
only after deliberate user action, not after a failed metadata read.

Confirmations capture the operation, provider/source/import/publication, displayed revision and
actual expected tokens. Closing/replacing the parent or changing that target invalidates only its
confirmation. Rechecking a disabled state only in markup is insufficient; all effect handlers must
validate current origin and enforce native policy. Do not bypass authorization for the sandbox.

## Existing native contracts remain authoritative

SharedProviderManagementService uses typed requests with source/import/provider concurrency tokens
(R18). SharedProviderRecovery, target/source attempts, target verification and ChangeDelivery already
model important states (R13/R15/R25). Reuse or adapt them at the production seam. Do not duplicate
their protocol inside the renderer or introduce another persistent recovery ledger.

Publication/save/test/synchronize can commit and then fail delivery/read-back. Record native facts
before checking whether the view can still publish. A detached renderer cannot retract a committed
source update. Retrying a known pending delivery never repeats the remote action, increments imports,
issues a credential or diagnoses again. Unknown results retain exact candidates and require canonical
verification. A determinate refusal/conflict is not automatically Unknown. Preserve separate local
alias and remote source availability/metadata outcomes.

Avoid promising exactly-once across HTTP failures unless the current native owner supplies it.
Test dropped response before/after known write, stale reviews after a newer request, attempts
closed/reopened, two circuits, unavailable canonical state, partial affected-import sets and
pending callbacks. Source list refresh is not an authoritative verification of every mutation.

## Rendering-specific inherited risks to characterize in PP2

The imported child is currently keyed by `(ImportId, ImportConcurrencyToken)` (R12), so a token
change remounts its local draft. Prove that a remote-only metadata update during dirty alias editing
does not silently discard edits; use explicit conflict or field merge according to original owner.
The source dialog writes returned identity to a component field and closes it after awaiting (R15).
Prove it is still the exact edited source/attempt; another dialog/view must not receive that result.
These are required lifecycle tests, not preclaimed runtime bugs or license for broad redesign.

Native source retry/delivery knows about multiple affected providers. Do not ack an undelivered
change merely because a selection changed. It can refresh metadata for the right scope while
preserving drafts; failed refresh must remain a pending delivery when the contract requires it.

## Sensitive data

Render only reference IDs/names, safe endpoint/status and public metadata. Credentials and bearer
values belong to existing operator/vault flows, never generic receipts or state snapshots. Untrusted
URI/status/error text must use current native validation/sanitization. Native root paths, tokens,
HTTP auth headers and executable command diagnostics stay out of committed evidence.

No authority is derived from a visible display name or a caller-supplied environment variable.
Preserve network redirect/private-address/source identity checks and upstream credential isolation.
The fixture may explicitly allow its own private HTTP source; that is not a global network-policy
exception. Do not relax runtime imported profile constraints or future source availability rules.
