# Dependency and ownership design

## Preserve the established direction

```text
Production Workspace / Web composition
  +-- Core UI                      -> Core contracts + existing neutral UI
  +-- API Access UI                -> API contracts + neutral UI
  +-- Storage Catalog UI           -> Catalog contracts + neutral UI
  +-- Storage Selection UI         -> Selection contracts + actual AppComponents
  +-- Storage Recovery UI (new)    -> narrow recovery view contracts + neutral UI
  +-- Data Sources UI (new)        -> narrow profile/transfer view contracts + neutral UI
  +-- trusted configuration host  -> approved registry + neutral Configuration.UI

Each independent sandbox --------> its actual leaf + deterministic scenario ports
Existing runtime owners <--------- narrow production adapters, not the reverse
```

Suggested new roots, not mandatory names:

`src/Modules/CanDoItAll.Modules.Workspace.StorageRecovery.Contracts`
`src/UI/CanDoItAll.Workspace.StorageRecovery.UI`
`src/Sandboxes/CanDoItAll.Workspace.StorageRecovery.UiSandbox`

`src/Modules/CanDoItAll.Modules.Workspace.DataSources.Contracts`
`src/UI/CanDoItAll.Workspace.DataSources.UI`
`src/Sandboxes/CanDoItAll.Workspace.DataSources.UiSandbox`

A separate Presentation assembly is justified only by a real dependency boundary. Pure presentation controllers may live in the relevant UI project. Do not impose interface, project, class or file-count quotas. A renderer must not call DbContext, resolve a service provider, instantiate a runtime driver or use a hidden HTTP client as a substitute for a real boundary.

Core/API/Catalog/Selection projects and their existing sandboxes must not acquire Recovery or Data Sources references. Foundation, MAF, AppComponents and external reusable packages must not point to Workspace product contracts. Keep the broad production composition graph where it belongs; don't contaminate a lean UI graph to make the startup code convenient.

Do not move entire mixed Infrastructure contract files to product modules. Recovery public API types and Data Sources runtime options coexist with broader implementation types. Prefer safe UI projections and explicit mappings at the production adapter. No database profile password, signing key, protected payload, authorization stamp, live DbContext, stream or service delegate belongs in a serializable navigation record.

## State and effects

A field revision is not a target identity. Preserve confirmed IDs/versions separately from reconciliation of later edits. Capture the request before the first incomplete await; after every relevant await, check the original operation/lifetime before publishing UI effects. Blazor can re-enter at incomplete awaits even within one logical circuit thread. [EX01]

Keep independent read lanes for independent datasets. It is acceptable to serialize all actions within the cohesive Recovery dialog if that preserves its current operator contract; do not manufacture unnecessary concurrency. A busy control is not backend enforcement or handler admission. Cancellation ends observation/ownership, not a known committed effect.

Use known-rejected / committed / committed-with-follow-up-warning / genuinely unknown outcomes at the real owner boundary. A void or generic failed result does not prove rollback. Do not retroactively infer commit causality solely from finding a same-name record. Adopt a known returned identity before optional secondary reads. Recovery/transfer stages may legitimately have mixed results; retain those distinctions rather than flattening them into a success boolean.

Receipts must be bounded and redacted. Never keep full password-bearing commands or connection strings in an operation ledger. Never evict unresolved effects solely to free capacity. Existing exact recovery protocols are preferable to inventing generic persistent command infrastructure.

## Assets and lifecycle

Move the actual rendered subtree and necessary CSS/JS/asset ownership. Reuse real BaseLib and Configuration/UI components, not lookalike markup. Resolve dynamic renderers through the existing approved registry at the host. Existing control names, route tokens and serialized wire contracts remain compatible unless a justified local mapping changes only the UI type.

Read source and evaluated graph, assembly closure, public exposed types, static web assets and watch inputs separately. A bare ProjectReference scan is not sufficient because sibling packages are replaced by local source. Watch traverses project references; independent sandboxes provide the focused development loop, not a promise that the full Web host becomes small. [EX02]
