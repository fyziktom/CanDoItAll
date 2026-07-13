# C# Pattern Selection Records

## PSR-01: Storage Browse Sidecar And Registry

- Problem force: existing `IStorageDriver` has a closed read/write/delete contract; providers support different browse/search/stat capabilities.
- Selected: separate provider interface plus typed registry/catalog.
- Rejected: enlarge `IStorageDriver` (forces false support), giant switch (extension edits), service locator (hidden dependency), partial driver files (fake boundary).
- Test seam: instantiate registry with fake browse drivers; duplicate/unknown provider negatives.
- Proof: adding another browse provider requires registration/type only, not editing the registry.

## PSR-02: Provider Adapters

- Problem force: filesystem/IPFS/FTP details and FileTools types must not leak into native contracts.
- Selected: provider-specific native browse adapters plus an outer Adapter from `StorageBrowse*` to FileTools provider contracts.
- Rejected: return FileTools DTOs from Infrastructure; duplicate path/transport logic; make UI parse storage locator formats.
- Test seam: fake native browse driver under outer adapter; fake path/transport collaborators under provider implementations.
- Proof: Infrastructure source/project audit has no FileTools symbols/reference.

## PSR-03: Cache Decorator

- Problem force: optional cross-request listing reuse with identical behavior and explicit Disabled pass-through.
- Selected: Decorator around native/outer browse service with typed policy resolver and revision key.
- Rejected: cache inside FileBrowser session/provider, page-local dictionaries, silent fallback from unavailable Hybrid to Memory.
- Test seam: fake inner provider/counting cache; prove Disabled never touches cache and scoped keys do not leak.

## PSR-04: Opaque Handle Registry

- Problem force: browser state and unsigned tokens cannot carry authority; effects need expiry, actor/runtime binding, revocation, and operation constraints.
- Selected: bounded server-side registry keyed by cryptographically random typed handle ID.
- Rejected: signed display DTO as sole authority, raw path/CID/token, client-side capability check.
- Test seam: deterministic clock/random abstraction only where required by tests; direct stale/cross-scope/revocation cases.

## PSR-05: Thin Module Coordinators

- Problem force: existing pages/dashboard/partial cluster are oversized and UI lifecycle must remain predictable.
- Selected: focused application/coordinator services and focused Razor panes/dialogs/windows; parent holds minimal render state.
- Rejected: another page partial, nested helper class, shared `FileManager`, or broad facade owning behavior.
- Test seam: instantiate coordinators/scope providers without the page; component tests supply fakes.

## PSR-06: Existing FileInteraction Builder

- Problem force: renderer/profile packages must be selected explicitly.
- Selected: use FileTools `FileInteractionComponentBuilder`; add only host adapters/renderers actually required.
- Rejected: second app-specific builder, service-locator renderer selection, global switch over extensions.
- Test seam: build composition directly; ambiguity/unsupported/selected package tests.

## Simpler Options Retained

- Do not add Strategy/Command/Factory types where a cohesive class and constructor injection suffice.
- Resource promotion may be a focused application service method rather than a formal Command hierarchy.
- Module-specific scope providers remain in their modules; do not create one implementation project per user story.
