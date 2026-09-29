# Storage catalog UI architecture and forbidden dependency directions

## Independent leaf

Recommended, not mandatory naming:

```text
Workspace implementation / production adapter
    -> Workspace.StorageCatalog.UI -> Workspace.StorageCatalog.Contracts
              ^                                 (safe projections / narrow owner ports)
              |
Workspace.StorageCatalog.UiSandbox -> deterministic scenario owner
```

Paths:
`src/Modules/CanDoItAll.Modules.Workspace.StorageCatalog.Contracts`,
`src/UI/CanDoItAll.Workspace.StorageCatalog.UI`,
`src/Sandboxes/CanDoItAll.Workspace.StorageCatalog.UiSandbox`.
Add a separate presentation assembly only for an actual ownership/substitution benefit.
The API precedent shows cohesive controllers can live in a light Razor assembly. No quotas.

The production Settings route keeps its generic slot and composes a thin Storage host.
That host owns profile/caller integration, local notifications and the Recovery dialog.
Catalog renderer raises a typed recovery request with the exact selected persisted ID (or
explicit catalog-wide null). It never imports IStoragePlacementRecovery or continuation
runtime. Its sandbox labels recovery as a deferred host action; it must not mimic a completed
recovery or claim the actual dialog is extracted.

## Non-negotiable graph rules

| Origin | May depend on | Must not gain |
|---|---|---|
| Core UI/contracts/presentation/sandbox | Their current legitimate dependencies | StorageCatalog or API sibling leaf/runtime |
| API UI/contracts/sandbox | Current API contracts/components | StorageCatalog, general Workspace runtime |
| StorageCatalog.UI/sandbox | Its light contracts and actual shared components | Workspace implementation, Infrastructure, Security implementation, Projects/Agents/Processes/Workbench implementations |
| StorageCatalog.Contracts | Necessary genuinely light types only | EF, DbContext, driver/secret resolver, runtime composition, another feature's broad facade |
| Infrastructure / Storage owner | Current foundation dependencies | A product Workspace.*.Contracts/UI reverse reference |
| Production Workspace host | Existing owners plus new leaf | A universal Settings controller/service-bag or new ambient service locator |

Check evaluated MSBuild/package-source replacement, runtime closure and public generic/event/
parameter types. A renamed file or package reference can still carry a heavy edge. Negative
controls must fail on forbidden, unresolved and cyclic dependencies. Do not merely broaden
allowlists until tests pass. Preserve Core/API graph/watch baselines independently.

## Project only what the wizard renders

ST03 types contain Infrastructure storage enums and helpers. Prefer a bounded UI-specific
projection rather than moving all Storage runtime contracts upward. Existing picker/service
consumers can retain their types. Public names/wire/defaults must remain stable where consumers
actually rely on them. Document mapping coverage, enum/flag conversion and unsupported values.

Runtime owns bootstrap identity, source fingerprint, configuration serialization, host binding,
routing semantics/capabilities and credential resolution. UI receives editable values,
reference-only secret choices, current metadata and narrow commands/results. Do not pass
StorageDriverInput, IStorageDriver, raw service providers, grant handles, or secret material.
Canonical definitions/templates/labels can be projected from current owner helpers once; do
not duplicate driver capability policy into a second UI engine.

No new universal storage DTO migration. No change to schema, routing algorithms, cryptography,
FileTools authority, project admission, database switching or placement recovery. A narrow
owner method/result for exact editor acquisition or acknowledged partial stages may be added
where required. Keep non-UI APIs compatible and widen tests to real consumers.

## State ownership

Keep catalog/reference availability, exact editor acquisition, mutable draft/EditContext,
wizard step, selected target, committed outcome and health-test origin separate. Do not use
a successful list fetch as permission to edit a placeholder. Optional reference errors keep
known IDs and raw text rather than substituting another secret/provider.

Capture a complete deep command before awaiting: provider, connection mode, all connection
fields, selected secret ID, flags, display order, routing list, stored ID, database origin and
necessary private owner context. Snapshot fields must not be read back from the live editor.

One operation lifetime does not own every read. Receipt identity and per-field/destination
versions reconcile acknowledged state without replacing newer text or lending another draft
an ID. Share actual controller behavior with the sandbox; do not fork an optimistic fake UI.

## Performance

No owner list/driver/secret calls from rendering getters, keypresses or wizard transitions.
Pure preview/template calculation is allowed. Catalog/routing queries currently overlap;
measure and avoid introducing extra round trips, but do not add cross-profile global caches.
Connection tests remain explicit. Bound receipt/history/scenario state and release leases,
subscriptions and CTS sources even on superseded/denied/disposed paths.
