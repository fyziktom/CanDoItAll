# State, intent, lifetime, and routing readiness

## State taxonomy

| State | Owner/storage |
|---|---|
| Durable selection, meaningful view, committed filter | Semantic workspace instance; later eligible for location |
| Active editor/overlay target and section | Semantic workspace instance, separate from list selection |
| Mutable draft, validation and edit context | Editor session; never the URL |
| Loaded data and regional failure/loading state | View/session data, keyed to its request |
| Preferences | User/profile store below explicit location precedence |
| Geometry | Local/workbench layout state |
| Busy flags, request generations, dismissed request, focus | Transient instance state |
| History provenance / parent-entry marker | Host history, not durable object identity |
| Secrets/confidential content | Never shared location state |
| Scenario selection | Development host only |

A single owner means one authority per state, not one giant record or one giant class.
An effect host must not become a second authority for route-significant selection. It may
mirror input for compatibility and own an opening acknowledgment, pending operation or
presentation lifetime. Document when its mirror changes and how it reports changes to the
workspace. A request A -> null/missing -> A must clear request-owned selection and rearm
opening; an unchanged A echo must not create another editor. Cover both through public
parameters and the actual page callback path.
Do not place mutable drafts or request generations in serializable navigation state.

## Controlled contracts

Children receive typed values and emit callbacks/intents. They do not parse or construct
their parent's URL. A genuine cross-page link can receive a precomputed Href.
Use a direct callback for a simple change and a cohesive intent family for related
transitions. Avoid both callback explosions and a global untyped event bus.

Selection is not necessarily editor opening. Create, edit, no editor, and missing target
must be distinguishable. Use stable section identity with explicit rendering/token maps.
Neither numeric index nor localized label nor enum.ToString is an external contract.

## Session and effect lifetime

Before extraction, specify:
- Same entity + section change: retain draft/edit context; load only required lazy data.
- Different entity / create / clear: explicit session transition and existing dirty policy.
- New save: update durable identity and expected version without unintended session reset.
- Close/delete: clear the matching active request; preserve current result-channel behavior.
- Request A superseded by B: late A data, errors, notifications, and completions cannot
  update B or reopen a dismissed session.
- Dispose: cancel owned reads, detach subscriptions, and observe command completion safely.
- Multiple instances: drafts and request keys must not leak through circuit-scoped services.

Keep workflow services stateless unless an explicitly created/disposed session owns state.
A DI scope is not a component/editor lifetime. Effect-owning catalog hosts need this
protection as much as editors: pass a lifetime token to supported reads, mutations and
launchers; observe dialog waits; fence completion after each await and suppress only
owner cancellation or stale-instance publication. Cancellation does not undo committed work.
Use a request generation for superseded requests and a load generation for overlapping
snapshots when those operations can overlap; do not put these in semantic workspace state.

## Transitional host contract

Current URL compatibility remains at the outer adapter. In-memory controlled state may
precede route binding. A supplied initial session is optional only when it represents a
real use case; otherwise a fake of the production loader gives deterministic scenarios.
If accepted, define ownership, deep-copy needs, target matching, precedence, and updates.

The existing BaseLib DialogService closes dialogs on LocationChanged. Later URL-driven
section changes must not accidentally close/recreate an editor. Prefer an existing
declarative Dialog or a small explicit host boundary where suitable. Do not change global
dialog navigation policy incidentally. A forwarding wrapper is rejected; a host owning
lifetime, result channels, focus, or location adaptation can be a real seam.

## Decisions still open

The meeting pack is proposal evidence. Final paths/query keys, selection/open encoding,
Push/Replace policy, dirty-navigation changes, Workbench logical/artifact identity, and
MAUI implementation remain owned by navigation decisions. No canonical route migration
or new user-visible navigation behavior is authorized by a seam bundle implicitly.

Semantic-state readiness requires known transitions and one owner. Route-binding readiness
additionally requires a viable lifetime/host adaptation and no ownership redesign. Neither
claim means refresh, Back/Forward, or bookmarkability has been implemented.

A canceled wait does not necessarily cancel or release the underlying effect. Pass lifetime cancellation into the effect owner itself (for example, DialogService.OpenAsync), so disposing a host removes its own global-host presentations as well as fencing result callbacks. Preserve unrelated dialogs; a global CloseAll is not an ownership boundary. Test disposal/remount with the same semantic target and an unrelated concurrent presentation.

Stable section definitions should own labels and presentation ordering while semantic sections retain typed identities. Render from those definitions and map indices explicitly; enum ordinals must not become URL or persistent state contracts.

## Initial reads, refresh and nested presentation ownership

Initial task-slot ownership, latest accepted snapshot generation, and presentation loading are separate responsibilities. A stale initial completion must release its own task slot even after a newer reload wins; every accepted catalog must finish loading. Guard synchronous completion when assigning a task slot. If create remains available during initial loading, prove save/reload overlap with both late success and late failure.

Every directly opened nested dialog belongs to the actual editor/session lifetime. A token on a top-level dialog does not cascade to independently registered dialog references. Pass the captured session token to confirmations and wizards; prove both replacement and disposal remove only owned dialogs while preserving unrelated presentations.

For an unrouted list/editor panel, one explicitly constructed session can own the typed semantic state and draft/read lifetime. A page store is not mandatory. Tree/child identities derive from that authority. Initial automatic selection cannot override an explicit New or selection made while loading. A catalog that removes a pending target must prevent its late editor result becoming editable.

## Operation and target ownership

Busy state, pending reconciliation and completion belong to a captured target session and
operation generation. An older operation cannot clear a newer busy state or publish its
notification. Capture an independent submission before the first await; reconciliation
must distinguish edits made later from submitted values.

Target-changing child components cancel prior loads, clear obsolete actionable snapshots,
verify returned identity, and fence success and failure after each await. Overlay closure
cancels only its owned requests and presentations. Apply the same fence in caller
continuations that display notifications or open another presentation after a helper returns.
Backend commit receipts remain valid even when their presentation owner disappears.
