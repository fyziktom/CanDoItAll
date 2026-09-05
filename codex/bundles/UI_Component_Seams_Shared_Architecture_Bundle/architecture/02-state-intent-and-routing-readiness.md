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
A DI scope is not a component/editor lifetime.

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
