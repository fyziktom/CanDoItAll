# PP2C-R1 — retained local values with freshly adopted write tokens

## Evidence and scope

Source-derived finding in current Sharing UI; not executed by the reviewer.
`SharedProviderManagementPanel.razor` now keys the imported child only by ImportId and preserves it
during a same-target read. The child initializes `editModel` only when that ID changes. On every
read the parent replaces `profileState`; `SaveImportedProfileAsync` takes the child's alias/enabled
values but the newest `profileState.Import` tokens. The native owner checks exactly those supplied
tokens, then writes both alias and enabled [S05–S08, S11].

Keeping a dirty alias during metadata refresh is correct. Pairing its older local values with an
unconditionally newer authority baseline is not. This is a UI concurrency/lost-update risk, not
an authentication bypass, remote-model routing defect or already demonstrated database corruption.

## Minimal clean reproduction

1. Create an imported provider through native owners. Let import/provider revisions be I0/P0,
   local alias `Team model`, locally enabled true. Load its Sharing view in editor A.
2. Native owner/editor B changes the same import to alias `Operations model`, enabled false.
   Retain its returned I1/P1 and verify its saved state.
3. Refresh A through the real PP1 toolbar/Sharing parent revision path. Confirm the accepted
   Sharing snapshot contains B's new values/tokens and the child is the same instance.
4. In the current implementation A still displays its old alias/enabled values; click Save local
   settings without editing or deliberately reverting anything.
5. Capture the actual request: stale `Team model`, true are paired with I1/P1. Native concurrency
   accepts them and B's change can be overwritten. Verify exact persisted state and no unrelated
   provider modification. This is the failing test to demonstrate, not a prefilled test result.

## Dirty and own-commit variants

Repeat with A's alias already edited before B acts, and with B changing only enabled while A edits
only alias. Also cover B changing alias while A changes enabled; an unchanged local field must not
be silently reverted. Repeat while A is typing without blur, after A-B-A target navigation, and
across current/late read failure. Include A's successful Save that normalizes its alias, followed
by another local edit while the returned commit or delivery waits. Do not infer publication or
local identity from the current row index.

## Required semantics (choose the smallest coherent design)

Maintain distinct facts: accepted remote metadata, initial local writable values and native token
pair, live draft/raw validation, one immutable submitted attempt, and confirmed owner outcome.

- Clean local draft: fresh local data may replace its local values and baseline coherently.
- Dirty local draft, remote-only change: preserve text/context and update remote facts. A baseline
  may advance only through a proven safe policy, e.g. matching the accepted local writable values
  against that draft's original local values. A newer opaque token alone is not proof of safety.
- Dirty local draft plus another operator's local change: retain edits and preserve a real conflict
  or show an explicit review/merge choice. Do not silently supply the latest tokens with old content.
- Own confirmed Save: reconcile against submitted values, preserve later typing, adopt only the
  appropriate confirmed identity/version, then deliver/review without repeating the write.
- Known refusal versus unknown: native conflict remains a known refusal. Actually unknown work
  retains original attempt/recovery and cannot be blindly retried.
- Explicit operator override after reviewing fresh state is a new deliberate submission, not a
  hidden consequence of Refresh. Do not silently save before switching/refreshing.

Do not restore unconditional child recreation keyed on every remote token, disable all refresh,
force Save before metadata queries, add last-write-wins, create a global lock or new persistent
concurrency protocol, or weaken native `EnsureConcurrency`.

## Proof layers and positive controls

Use actual parent/child events, then real PostgreSQL/native management read-back with two owners.
A mock that merely asserts the chosen token cannot establish lost-update protection. Confirm the
native old-token conflict independently, normal clean/dirty saves, remote-only metadata update,
first acquisition/retry, catalog absence, read failure, duplicate dispatch and original identity.
Retain the existing `Remote_metadata_refresh_keeps_the_separate_dirty_sharing_alias` behavior.
Extend it to submitted values/authority, not only visible text. After extraction repeat the
critical clean and dirty cases in two browser circuits on the same client instance.
