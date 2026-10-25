# 08 · UI drafts, conflicts and postcommit reconciliation

## Reuse existing seam semantics

SRC-02 already defines host-held drafts/EditContext, generation-fenced reads, one mutation per editor lifetime, submitted/live reconciliation and saved-with-warning behavior. This plan extends those semantics to stale content; it does not create a competing UI-state framework.

The routed host/session owns the base snapshot, mutable draft, submitted snapshot, expected stamp and outcome. The renderer receives data and typed intents. It may render changed-field/conflict states but never accesses an owner DbContext. No live mutable draft is placed in singleton/circuit-shared services or serialized navigation state. EF's circuit-lifetime cautions reinforce this separation (EXT-07).

## Four distinct snapshots

| Name | Used for | Ownership |
|---|---|---|
| Base B | What this editor originally read | Frozen for conflict comparison |
| Live local L | What the operator currently has typed | Mutable current editor instance |
| Submission S | Exact normalized/unmodified input dispatched for this operation | Frozen when save begins, independent from L |
| Current/accepted C | Either the safe current owner state after a conflict, or exact accepted commit result | Label which of those meanings applies |

Conflict resolution uses **B/L/current C**. Postcommit reconciliation uses **S/current L/accepted C**. Mixing the two algorithms loses text typed during an in-flight save. This distinction is explicitly established by the current draft reconciler guidance (SRC-02).

## Editor state machine

```text
loading -> ready-clean -> editing-dirty -> submitting
                              ^              |
                              |              +-> known-rejected / conflict
                              |              +-> committed -> reconcile accepted state
                              |              +-> unconfirmed -> resolve original operation
                              |
                         explicit correction/new proposal

Any target change retires the old request generation.
A late completion may resolve its operation, but cannot mutate the new editor.
```

A committed result can belong to an editor that has since been retired. Preserve the durable operation outcome in the appropriate owner/session tracking without reopening an old dialog or replacing the newly selected target. Cancellation requests affect owned reads and waits; they do not prove a dispatched write was rolled back.

## Basic conflict UX for the pilot

Preserve the live draft and raw validation state. Present a clear explanation that saved data changed since loading, and let the operator inspect changed fields and collection items. Offer a safe current-state read and a deliberate conflict resolution. Do not show raw EF exception text or database values outside the caller's read permission.

Start without automatic collection merging. For independent scalar fields the three-way rule can be proposed: unchanged locally takes current; unchanged remotely takes local; equal new values need no choice; competing differences require resolution. The owner validates the final result as a new conditional command. Cross-field invariants can still forbid a syntactically clean merge.

"Use my change" means a new explicit proposal against the state actually shown, not a global `Force=true`. A third update between the conflict display and resolution can conflict again. Never disable the expected-stamp check to avoid repeated prompts.

## Collections and server normalization

Phases/options require stable identity, not array index. Distinguish a persisted identity from a temporary local row. The accepted result must map newly created rows back to their local submission so a second save does not delete and recreate them. Removing a row while save is pending is another local intention; reconcile it only according to the defined policy.

Normalization includes trim, UTC treatment, slug generation, default-category materialization and owner-generated IDs. A clean postcommit reload can reset an unchanged draft; if the user typed since submission, keep those edits and their EditContext, adopt accepted values for untouched fields, and replace the expected stamp with the accepted version only in conjunction with that reconciliation. A mere token replacement with an old mixed draft is not sufficient.

If the accepted exact snapshot is not available and read-back returns a newer revision, do not present that as what this operation committed. Use explicit subsequent-change handling. A minimal safe pilot may disable the edited form while saving, as the Collaboration extraction does, but must deliberately preserve each feature's existing UI contract rather than universally freezing every editor (SRC-13).

## Notification policy

No realtime channel is needed for correctness. Later, per-owner invalidations can mark the same loaded target as stale. A clean editor can refresh; a dirty editor shows an unobtrusive warning. Do not auto-merge incoming notifications into a dirty draft. Coalesce bursts, bound subscriptions, unsubscribe on disposal and keep unrelated panels independent.

Notification payloads should contain safe identity/stamp metadata, not the whole confidential record. Missing, delayed or duplicated notifications must not alter write acceptance. Reconnection triggers a targeted read, not a full-project graph refresh for every card.

## API-only frontend parity

The alternative API UI must be able to reproduce the same outcome distinctions, original expected stamp, operation ID and draft-preserving conflict flow. Do not put all meaningful data in a server-only JsonIgnore property and assume the public API has equivalent protection. The HTTP adapter translates to the same owner contract; it does not own a different merge policy.

## Tests and architecture guard

Use deterministic session/renderer tests for conflict states, post-dispatch typing, A → B → A, request cancellation ignored by a fake, delayed error, disposed view, nested dialogs and duplicate submit. Use real PostgreSQL owner tests independently; a fake returning Conflict only proves the presentation response to that value. Playwright then covers the real two-editor/agent interaction, readable warning, retained text, focus and row identity.

Extend existing evaluated dependency and public-signature guards so an extracted renderer cannot pull in the new persistence/token implementation indirectly. Asset/route/sandbox registrations remain part of the extraction. Do not assert a fixed number of helpers, files or interfaces.
