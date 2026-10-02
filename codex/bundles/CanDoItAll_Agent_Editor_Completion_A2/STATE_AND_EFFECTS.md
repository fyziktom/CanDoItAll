# State and effects: acceptance criteria across all ten sections

## One editor, separate lanes

Keep one complete draft/EditContext/session per editor. Section navigation preserves it; a new
explicit target/create retires it. Catalog selection and active editor target are not the same.
Reference lists, add-row candidates, confirmation requests, accepted mutations and proof review
have their own state. A global 'loading' or one large operation bool must not merge unrelated work.

Same session does not mean same request. Current `LoadAsync` and several reference paths mainly
fence by session. Characterize overlapping retry/current same-editor reads; latest selected target,
local text and actual first acquired editor must not be replaced by an older request. Add a minimal
per-lane guard where required, including error/finally, not a global serialization lock.

Callbacks carry the rendered origin and relevant row/reference/operation identity. A->B->A is not
revival of A's old callbacks. A tab echo is not a new editor. An older operation owns its own CTS
until completion; component disposal cancels but does not prematurely dispose resources still in
use. Cleanup always detaches and releases the exact old owned resources, not current shared fields.

## Edits and acknowledgement

Save captures every field before dispatch: A1 plus all new sections, tags, hidden settings,
versions, selected IDs/lifetimes/bindings and opaque JSON. Raw input is captured through actual
input events, including unblurred text and invalid values. Check full-form validation; a hidden
section must not lose its raw error or accidentally bypass validation.

A commit adopts actual returned identities/version and accepted unchanged fields. Preserve later
input and the same context. For nested collections preserve both row identity and newer edits.
Treat no changes after a Save differently from no changes after Verify: Verify never submitted the
configuration draft. Do not use 'latest version' to silently rebase unrelated concurrent changes.

Unexpected command exceptions are characterized at the actual phase. A definitely rejected pre-
write capture is not an unknown commit; an admitted write with unknown confirmation is not
safe to repeat just because a generic catch reset `isBusy`. Use existing typed owner outcomes.
Avoid a new durable protocol. A bounded repair is allowed with precise failure injection and tests.

## Subforms and references

An incomplete memory binding or root candidate is an edit owned by that subform. Make Add/Remove,
Enter and parent Save semantics explicit. Preserve same-session candidates on unrelated re-render;
reset only on actual target retirement. Missing reference IDs must not be dropped because current
metadata is absent. Reference load success, freshness, native authority and user intent are distinct.

Typing should not trigger O(catalog size) backend work or driver/registry construction repeatedly.
Preserve legitimate memoization and lazy behavior; do not introduce an unbounded shared cache.
Bindings remain keyed by stable IDs/aliases, not labels or list positions.

## Partial effects and data sensitivity

Save -> CRM/cache warning -> read-back, capability create -> assign -> read-back and diagnostic ->
publication -> review are multi-stage operations. Retain exact known stages and native identities.
Read retry does not execute writes/diagnostics. A superseded editor may discard presentation but
cannot claim that a committed native effect never occurred.

Public diagnostics contain safe IDs/stages/codes. No secret values, provider authentication options,
protected root tokens, full agent JSON or filesystem credential paths in generic UI receipts/logs/
recorded screenshots. Use private task data and sanitized manifests. Raw private artifacts remain
separate; passing a curated scan never certifies all raw outputs publishable.
