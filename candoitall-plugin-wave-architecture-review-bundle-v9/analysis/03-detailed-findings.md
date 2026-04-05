# Detailed findings
## P9-001 — Legacy carrier fields still exist on ProjectObjectRecord and Workbench_ProjectObjects
Severity: **Critical**  
Gate: **HG-01**  
Status: **Open**  
Repeated offender: **Yes**
### Evidence
- `src/CanDoItAll.Modules.Workbench/ProjectObjectRecord.LegacyCarrier.cs` lines 3-17: Route / ExternalArtifact* / Media* / StorageObjectReferenceJson still live on ProjectObjectRecord.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs` lines 10-26: Legacy carrier columns are still declared as required ProjectObject columns.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs` lines 50-67: Workbench_ProjectObjects CREATE TABLE still persists legacy carrier columns.
### Why this is bad
The node carrier is still polluted by transport / binding concerns. New plugins will keep pushing external-identity details into the core node entity, so the universal carrier remains leaky and migrations remain expensive.
### Required change
Retire the legacy carrier fields and DB columns from ProjectObjectRecord / Workbench_ProjectObjects. Keep binding state only in ProjectNodeBindingRecord (or an equivalent binding facet table) and compose it only in dedicated read models at the edge.
### Closure evidence expected
ProjectObjectRecord no longer exposes the legacy binding fields, Workbench_ProjectObjects no longer stores them, and no active code path reads or writes them.

## P9-002 — Binding layer still hydrates legacy carrier state back into the node runtime model
Severity: **Critical**  
Gate: **HG-01**  
Status: **Open**  
Repeated offender: **Yes**
### Evidence
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 283-296: Apply(...) writes binding data back into node.Route / ExternalArtifact* / Media* / StorageObjectReferenceJson.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 356-366: ResolveBinding(...) still falls back from binding state to legacy carrier properties on the node.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 392-400: HasLegacyCarrierPayload(...) still treats the node carrier fields as active payload.
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs` lines 71-92: Projection assembly still copies legacy carrier values into node.Binding.
### Why this is bad
Even after introducing a binding table, the runtime model still behaves as if the node owns binding truth. That preserves dual semantics and makes it easy for future code to accidentally depend on legacy carrier fields again.
### Required change
Stop hydrating binding values into legacy node fields. Resolve / compose binding data only in binding-specific or projection-specific DTOs. Remove fallback-from-node logic entirely.
### Closure evidence expected
ProjectNodeBindingStorage.Apply(...) no longer writes node.Route / node.ExternalArtifactKind / node.MediaRelativePath etc.; ResolveBinding(...) no longer falls back to node carrier fields; projection assembly no longer seeds binding from legacy node fields.

## P9-003 — Marker truth is still dual represented and normalized from legacy scalar fields on reads
Severity: **High**  
Gate: **HG-02**  
Status: **Open**  
Repeated offender: **Yes**
### Evidence
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` lines 40-43: ProjectObjectRecord still persists MarkerIcon / MarkerTone / MarkerLabel plus MarkersJson.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeMarkerState.cs` lines 43-76: ResolveLegacyJson(...) and HydrateLegacyFields(...) keep both representations active.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeMarkerState.cs` lines 78-109: NormalizeAndHydrateAsync(...) writes marker normalization during runtime.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs` lines 19-22: Schema initializer still requires scalar marker columns.
### Why this is bad
You explicitly treat markers as canonical analytical data. With both scalar primary-marker fields and MarkersJson alive, drift is inevitable and downstream analytics / similarity models can be poisoned by inconsistent marker state.
### Required change
Choose one canonical representation. The cleanest current path is to keep MarkersJson canonical and derive primary marker display data outside the persisted node entity. Remove scalar marker fields from persistence or demote them to non-persisted computed values only.
### Closure evidence expected
Only one canonical marker representation remains persisted. Read paths do not call ResolveLegacyJson/HydrateLegacyFields, and LoadAsync is marker-read-only.

## P9-004 — Provider and resource plugin editors are still hardcoded by field key and hardcoded editor properties
Severity: **Critical**  
Gate: **HG-03**  
Status: **Open**  
Repeated offender: **Yes**
### Evidence
- `src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor` lines 195-331: Resource editor renders fields via @switch(field.Key) across known keys only.
- `src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor` lines 271-295: Provider editor renders fields via @switch(field.Key) across three known keys only.
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs` lines 137-214: ResourceEditorModel is a hardcoded property bag for current plugins.
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` lines 113-143: ProviderProfileEditorModel is still a hardcoded current-plugin model.
### Why this is bad
The manifests can list fields, but the editors cannot truly render or persist unknown plugin-defined fields. Every new email / LinkedIn / custom API plugin will still require core page/model edits, which means the platform is not plugin-first yet.
### Required change
Introduce a generic connector configuration state bag and a generic renderer driven by ConnectorConfigFieldType. Known plugins may keep typed adapters, but the shared editor must round-trip unknown fields without page changes.
### Closure evidence expected
A test plugin that declares previously unknown fields of types Text / Url / Number / Boolean / Json / SecretReference can be rendered, edited, saved, reloaded, and validated without changing ResourcesPage.razor or SettingsPage.razor.

## P9-005 — Custom plugins still persist bogus legacy enum identity
Severity: **Critical**  
Gate: **HG-04**  
Status: **Open**  
Repeated offender: **Yes**
### Evidence
- `src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor.cs` lines 189-230: EnsureLegacyResourceKind / ResolveLegacyResourceKind still synthesize enum identity from plugin key.
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs` lines 363-370: SaveAsync persists entity.ResourceKind = connectorPlugin.LegacyResourceKind ?? model.ResourceKind.
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` lines 25-33: ProviderProfile still has active ProviderKind property.
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` lines 101-119: Provider summaries and editor model still expose ProviderKind as an active surface.
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` lines 313-316: SaveProviderAsync persists entity.ProviderKind = providerPlugin.LegacyProviderKind ?? model.ProviderKind.
- `src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs` lines 230-281: NewProvider(...) still defaults plugin identities through legacy ProviderKind presets.
### Why this is bad
Plugin key is not yet the single source of truth. Custom plugins can end up carrying fake legacy enum values, which will leak into summaries, reports, filters, or future behavior. That directly undermines the plugin platform.
### Required change
Demote ProviderKind / ResourceKind to compatibility-only optional fields or retire them. New/custom plugin flows must persist plugin key as the authoritative identity and must never synthesize a legacy enum just to satisfy old code.
### Closure evidence expected
Saving and reloading a custom provider/resource plugin requires only ConnectorPluginKey and config state; no fallback enum assignment exists in active save flows.

## P9-006 — Node reference model is still closed-world and requires core edits for each new relation
Severity: **High**  
Gate: **HG-05**  
Status: **Open**  
Repeated offender: **Partly**
### Evidence
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 8-22: ProjectNodeReferenceKind is still a fixed enum.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 59-67: ProjectNodeReferenceRecord.ReferenceId is still a Guid-only local identifier.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 108-148: ProjectNodeReferenceSet is still a fixed property bag for current relation kinds.
### Why this is bad
Each new plugin-defined relation (for example email thread, LinkedIn account, external contact, connector-owned object) still requires new enum members, new fixed properties, and core code edits. That is not compatible with a real extension platform.
### Required change
Move to an open reference model: namespace/key/target-kind/target-id/order/metadata, or an equivalent extensible facet model. Keep typed helpers at the edge, not as the core persistence contract.
### Closure evidence expected
A new plugin-defined node relation can be stored and queried without adding enum members or adding new properties to ProjectNodeReferenceSet.

## P9-007 — Read-time normalization still performs writes in the hot load path
Severity: **High**  
Gate: **HG-06**  
Status: **Open**  
Repeated offender: **Yes**
### Evidence
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs` lines 154-166: LoadAsync(...) still calls binding and marker NormalizeAndHydrateAsync(...) on reads.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 158-238: NormalizeAndHydrateAsync(...) still persists changes via SaveChangesAsync.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeMarkerState.cs` lines 78-109: NormalizeAndHydrateAsync(...) still persists changes via SaveChangesAsync.
### Why this is bad
Loading the graph still mutates persisted state. That hides unfinished migrations in the hot path, makes reads non-idempotent, and complicates concurrency and debugging. It also makes the final architecture harder to reason about.
### Required change
Move normalization to a dedicated one-shot migration/repair step. After the repair passes, delete the write-on-read logic from LoadAsync.
### Closure evidence expected
LoadAsync is read-only, and no normalization helper called from the load path saves to the DB.

## P9-008 — Write-side connector boundary is not yet ready for upcoming external plugins
Severity: **High**  
Gate: **MG-01**  
Status: **Open**  
Repeated offender: **Yes**
### Evidence
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchCrossModuleMutations.cs` lines 29-123: Current durable mutation model is scoped to internal project mutation records.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchCrossModuleMutationService.cs` lines 45-187: Current orchestration covers delete/move side effects, not a general connector command/outbox boundary.
### Why this is bad
Email, LinkedIn, and custom API plugins will introduce real external side effects. Without a generic connector command / outbox / retry / idempotency boundary, those plugins will likely couple UI/domain actions directly to external calls.
### Required change
Before shipping write-side plugins, introduce a generic connector command boundary with durable queueing, idempotency keys, retry/backoff, audit history, and optional approval hooks.
### Closure evidence expected
There is a generic connector command record + processor + tests for retry, idempotency, replay, and failure visibility, and write-side plugins execute through that boundary rather than directly from UI or workbench services.
