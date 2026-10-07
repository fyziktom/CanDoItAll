# State, effects and owner contracts

## Distinct identities

Retain separate native profile/actor context, workflow acquisition, immutable saved version, local
document revision, selected node/edge occurrence, overlay instance and effect attempt. An ID string
alone cannot distinguish A→B→A, deletion/recreation or two editors of the same workflow. Presentation
identity is not backend authority; every native write still uses its actual owner contract.

One draft owns the whole Workflow definition. Node and route forms edit that draft, not detached
copies that Save later forgets. One explicit operation admission covers alternate Save triggers;
replacing a UI read must not admit a duplicate durable write. Keep independent read lanes for
library/provider/secret metadata, selected definition, validation, preview projects and run details.

## Operation ledger (design guide, not mandatory new framework)

| Operation | Snapshot / completion rule |
|---|---|
| Load definition | Requested workflow/version + acquisition/profile; exact match, latest accepted read only |
| Edit node/edge | Original document occurrence and row ID; no callback into a replaced document |
| Validate | Captured immutable graph/input and revision; stale diagnostics are historical or suppressed |
| Save definition | Whole submitted snapshot and expected native version; confirmed IDs adopted before optional read/validation/callback |
| Create component | Exact Prompt/version and execution pair; component ID commit is distinct from draft binding |
| Prepare preview | Original graph/version/input editor; late project options cannot modify a successor dialog |
| Start preview | Exact submitted draft/input/simulation plan and native StructureAuthority; admission acknowledgement separate from result reads |
| Observe progress | Original run/attempt/document occurrence; no focus change in another active canvas |
| Add template draft | Exact selected template/version and current catalog owner; known new ID retained even if list refresh fails |
| Read run/event/content | Original authorized owner and selection; close/retire clears appropriate references and rejects late data |

## Error and completion classification

Known validation/concurrency rejection is not unknown. Known commit + failed read-back is not failed
Save. Cancellation of a rendered view is not rollback of a completed write or admitted run. Preserve
all accepted IDs and safe outcomes outside stale UI callbacks where the native operation already
owns them. Do not leak payloads, secrets, raw exceptions or host paths into generic operation logs.

Use existing native admission receipts and explicit reconciliation. An exact read recovery never
replays the write, creates a component again or launches another Workflow. Do not recover identity
by fuzzy name matching. If a specific operation truly lacks a recoverable acknowledgement, retain
Unknown and block blind replay; a new narrow owner result may be added only at that actual boundary,
with tests and compatibility for existing callers. No new universal persistence schema is requested.

## Context changes

Ordinary tab changes preserve the active document and raw inputs. Explicit workflow/new-draft/profile
transitions retire prior sessions and their actual callbacks. Save-induced own version advance is
not automatically a new user acquisition. The current page `@key` and callback capture must be
reviewed together; fixing only the child leaves a parent-driven draft reset possible.

Capture current profile/authority before dispatch, not after an unrelated await against a new
current-profile facade. Never widen the selected project scope. Do not force-switch the user's real
database, synthesize grants or remove old admission checks to simplify test fixtures.

Cancel/dispose owns only its own linked cancellation sources, JS modules, observers and listeners.
A source must remain usable while an accepted operation still needs it. Detach matching references
before asynchronous cleanup; old finally blocks must not clear new resources. Standard dialogs and
native canvas must keep their real ownership and cleanup; no global close-all or static shared draft.
