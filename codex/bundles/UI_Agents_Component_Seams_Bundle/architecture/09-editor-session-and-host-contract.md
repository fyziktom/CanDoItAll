# Editor session and host contract

This contract defines the required ownership and transition semantics; exact type names are illustrative. Implement it in SB04/SB05 only after baseline characterization.

## Identity and ownership

A catalog selection, an open editor target, and a loaded draft are different things. An editor target identifies existing entity or create flow; the host also distinguishes editor instances so two create flows or two open views do not share state. One editor instance owns its session/draft/edit context. A stable entity ID alone is insufficient to identify an asynchronous load generation.

Application operations are stateless with respect to mutable editor state. Blazor Interactive Server scoped services live across the circuit, so scoped DI is not an editor lifetime. Pass explicit requests/drafts and return typed outcomes; do not store CurrentEditor in a controller.

AgentEditorModel and nested settings are mutable. Do not alias a cached session, parent snapshot or fake fixture across instances. Choose one explicit owner and copy at the boundary when needed. Snapshot mutable input for an asynchronous write or deliberately freeze edits until it completes, preserving characterized UX. A completion must not erase edits made after its request snapshot.

## Required transitions

| Trigger | Required treatment |
|---|---|
| Open existing agent | New editor instance/session generation; load correct draft, references and ExpectedUpdatedAtUtc |
| Open create | New blank draft with an instance identity; never reuse another create editor's state |
| Same target, section change | Change semantic section only; retain draft, edit context, validation and reference state |
| New requested target | Follow characterized host close/open behavior; create a new generation and reject old load publications |
| Reference refresh | Update only its owned reference region; retain draft/expected version except explicit save reconciliation |
| Clear | Preserve current blank-model behavior and Identity section; synchronize target to create without accidentally clearing catalog selection |
| First successful create save | Bind returned persistent identity and latest concurrency token to the same editor instance; later save updates that entity |
| Existing successful save | Retain editor and section; reconcile persisted state/version while preserving any later local edits |
| Failed save / concurrency conflict | Retain recoverable draft, surface explicit error/conflict, do not force overwrite or silently reload over it |
| Committed save then failed refresh/callback | Report commit and refresh/callback state separately; permit refresh recovery without repeating mutation |
| Successful delete | Complete through current DialogReference result or non-dialog Saved callback once; host refresh reconciles selection |
| Close/dispose/reset while work pending | Cancel supported work and invalidate publication generation; committed external work is not undone by cancellation |

Preserve ExpectedUpdatedAtUtc from the original definition until a confirmed successful reconciliation. Do not replace a stale expected token with the current server token immediately before writing, which would defeat conflict detection.

Characterize reset, target changes, concurrent input while saving, close/escape and stale route requests before editing their ownership. Necessary identity/stale-publication safeguards are S rows; adding dirty-navigation prompts or changing Clear into discard/reload is a separate product decision.

## Host, result and callback rules

Preserve Save staying open and its current Saved/catalog refresh semantics. A delete result must not travel through two channels. Define the owner of notifications, refresh and completion for each outcome so moving code cannot duplicate them.

Host state needs semantic target/section transitions. If current DialogService parameter copying prevents a needed live parameter update, use the smallest meaningful host composition solution; do not mutate private DialogReference state or make a test-only API.

Current DialogService closes all dialogs on LocationChanged. No new navigation is introduced here. A future route-owned editor needs a host that retains the instance and updates semantic input across same-target navigation. Existing declarative Dialog is a candidate; a global library policy change is not assumed.

## Failure and test contract

Core-load failure B12 is a baseline uncertainty: inspect rendered controls and save eligibility, record the defect if any, and keep repair separate from refactoring authorization. Provider/secret/project errors already have separate regions; preserve them.

Test delayed old success and old failure after a newer target/reset/disposal, two concurrent editors, first-save identity, same-target section retention, conflict, and mutation-versus-refresh failure. Public fake load results should travel through the same production loading path. If a real production need justifies preloaded sessions, document precedence, cloning, rebind, identity mismatch and refresh invalidation before adding that API.
