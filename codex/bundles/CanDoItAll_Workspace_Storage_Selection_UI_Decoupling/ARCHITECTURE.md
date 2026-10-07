# Storage Selection architecture and containment

## Decision: a selection feature, not administrative Storage reuse

Create a leaf for the complete field/dialog selection family. The catalog-admin UI, API administration and Core Settings share a host route but not a state owner. The selection feature is consumed by an Agent editor, and must not inherit catalog-admin commands, receipts, write fences or runtime services just to list names.

Recommended layout:

```text
Agent / other production editor host
  -> StorageSelection.UI -> StorageSelection.Contracts
                       -> real neutral AppComponents and BaseLib children

Production composition -> narrow selection read adapter -> existing catalog owner
Independent sandbox    -> same StorageSelection.UI + controlled read-only fixture owner
```

A port may live in the UI assembly if it is purely presentation-specific; stable metadata reused by production adapters belongs in the light contracts. Do not add redundant forwarding layers. A view contract or presentation-record approach is acceptable when it clearly owns reads, child overlays and selected-list staging. Source state and consumer behavior decide, not an interface quota. [EV07]

## Dependency contract

| From | Allowed new dependencies | Forbidden new dependencies |
|---|---|---|
| Selection contracts | Framework and demonstrably light neutral types actually required | Workspace/Core/API/catalog-admin implementations, EF, drivers, vault, HTTP hosting |
| Selection UI | Own light contracts; actual neutral component families | Production Workspace/Agent module, Infrastructure, Security implementation, admin UI/controllers, Recovery |
| Selection sandbox | Selection UI and fixture data/state; parity CSS content | Production DI, Web/Composition project, database setup, Agent runtime, credential services |
| Agent/Workspace production host | Selection UI/port and existing owning services | A new cross-module manager or runtime authority implemented by UI |
| Core/API/catalog-admin UI and sandboxes | No new selection edge | Selection as a transitive consequence of a shared mega-contract |
| Foundation and MAF runtime | No new product selection edge | A reverse reference to Workspace.StorageSelection |
| AppComponents/RecordBrowsing/shared siblings | Remain feature-neutral | Any selection-specific catalog/Agent permission semantics |

Do not delete an existing production Workspace reference from another module until current source analysis proves all its other uses are gone. Reducing one child renderer's closure does not require claiming the entire Agent module is now independent.

## Read model

Project only what a picker needs: catalog ID, readable label, provider/connection display information, enabled/read-only/system flags and safe status/search text. Prefer immutable snapshot collections. Distinguish successful current metadata, stale retained metadata, initial unavailable and genuinely absent references. Selected IDs remain owned by the parent even when metadata is unavailable.

Do not expose `StorageCatalogEditorModel`, `StorageCatalogSaveRequest`, a driver input, a vault value, a DbContext or the complete old `StorageCatalogSummary` merely because it is familiar. The latter's current assembly/type dependencies are the reason a narrow projection is needed. Do not migrate all Infrastructure enums or canonical policies to a Workspace feature assembly. [WS05, ST06, ST07]

A context identifier records where a read belongs; it does not grant access. The production adapter retains actual current-profile checks using established facilities. A renderer cannot request arbitrary roots, switch databases or manufacture an authorized runtime scope. No persistent schema change is required.

## State ownership

| State | Owner |
|---|---|
| Agent identity, parent editor lifetime, allowed-ID list, AllowAll and other permission flags | Existing Agent host/session |
| Parent Save, normalization, storage authorization at execution/disclosure | Existing application/runtime owners |
| Temporary staged ID set and local query | Particular picker dialog |
| Read request/generation, metadata availability and source stamp | Particular selection read session |
| One open child dialog and its cancellation | Particular field/parent lifetime |
| Scenario/reset controls | Sandbox only |

An accepted selection result must be tied to the opening parent, policy/selection revision and dialog. A current value echo does not reset staging. A new owner carrying equal GUIDs does invalidate old callbacks. Normal close is local; no global `CloseAll` or shared mutable singleton selection state.

Use existing DialogService cancellation/ownership facilities. The current Agent risk-confirmation path demonstrates a supported cancellation-token argument and a captured owner check. It is a reference for lifecycle composition, not code to copy wholesale into a new permission framework. [WS08]

## Read and selection policy

Opening each chooser requests a current catalog. Existing saved IDs may resolve labels without opening it; empty fields need not load. Repeated renders, local search and staged toggles must not trigger backend reads. Profile/source changes retire caches instead of reusing old names against a new context. Dispose cancels/detaches owned reads and the owned overlay without clearing another instance.

The Apply gate validates live origin, source readiness and permitted staging. Newly disabled or fabricated unknown IDs cannot be introduced by a direct callback. Already-selected missing/disabled references are retained/removable, not silently discarded. Read-only selection remains permitted; write access is decided elsewhere. `AllowAll=true` preserves but disables explicit-list editing. `AllowAll=false` plus an empty list never implies broad access. [WS01–WS08, WS12]

No new mutation receipt ledger is warranted. The only durable mutation remains the parent editor's already-owned Save. Picker Apply must neither auto-save nor claim a committed grant.

## Compatibility and integration

Preserve stored GUIDs, boolean defaults, normalization and existing Agent HTTP/runtime serialization. The old Workspace source interface or result names may need a narrowly scoped compatibility facade for a proven remaining consumer. Do not leave the backend-dependent old implementation as the production renderer while claiming a sandbox-only extraction.

Update all real compiled Razor callers/imports, DI registrations, tests and any genuine reflection/assembly-name consumers. Keep read-source contracts independent of the parent editor's heavy runtime class. Do not pass `AgentEditorSession` into a portable picker; pass an appropriate light identity/revision/lifetime seam from the parent host instead.

Do not broaden policies of disabled catalogs, system storage, secrets, file roots or project lifetimes. Preserve existing `StorageCatalogService` bootstrap behavior through the real adapter, but do not repeat it per keystroke. New UI projection is not a reason to touch routing or host-bound path migration. [ST08, ST15]

## Assets and shared components

The current ResourceCardPicker is under AppComponents, with its own actual generic descendants. Its compiled closure is broader than BaseLib+one contract. Evaluate it rather than guessing. Keeping this neutral reuse is preferable to a clone or a simultaneous shared-family migration. [WS09–WS11]

Only isolate an additional generic component boundary if a real forbidden dependency prevents the required renderer from being used independently. Such a change needs explicit generic consumer and asset proof. It must not move feature data into AppComponents, relax boundary tests, duplicate renderer code or grow this task into a generic UI overhaul.

Move any owned CSS/JS with its actual component; preserve scoped CSS, asset paths, Tailwind inputs and published fonts/dialog/clipboard resources. A content link to generated parity CSS is permitted; a Web project reference is not.

## Performance and risk controls

Avoid per-render deep DTO reconstruction, whole-app service resolution, directory/driver probing or allocating a CTS for every search character when filtering is local. Keep request deduplication small and context-scoped. Do not cache forever across profiles. Bound scenario rows and held reads without truncating production selections or silently dropping saved IDs.

The S0 fix should reduce unnecessary exact reads, not introduce a broader cache policy. The picker graph will likely be larger than the admin sandbox because it reuses actual neutral components. Report actual measurements; no numeric performance target or guaranteed full-Web speedup is prescribed.
