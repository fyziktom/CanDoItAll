# Evidence map
## P9-001 — Legacy carrier fields still exist on ProjectObjectRecord and Workbench_ProjectObjects
- `src/CanDoItAll.Modules.Workbench/ProjectObjectRecord.LegacyCarrier.cs` lines 3-17: Route / ExternalArtifact* / Media* / StorageObjectReferenceJson still live on ProjectObjectRecord.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs` lines 10-26: Legacy carrier columns are still declared as required ProjectObject columns.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs` lines 50-67: Workbench_ProjectObjects CREATE TABLE still persists legacy carrier columns.

## P9-002 — Binding layer still hydrates legacy carrier state back into the node runtime model
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 283-296: Apply(...) writes binding data back into node.Route / ExternalArtifact* / Media* / StorageObjectReferenceJson.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 356-366: ResolveBinding(...) still falls back from binding state to legacy carrier properties on the node.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 392-400: HasLegacyCarrierPayload(...) still treats the node carrier fields as active payload.
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs` lines 71-92: Projection assembly still copies legacy carrier values into node.Binding.

## P9-003 — Marker truth is still dual represented and normalized from legacy scalar fields on reads
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` lines 40-43: ProjectObjectRecord still persists MarkerIcon / MarkerTone / MarkerLabel plus MarkersJson.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeMarkerState.cs` lines 43-76: ResolveLegacyJson(...) and HydrateLegacyFields(...) keep both representations active.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeMarkerState.cs` lines 78-109: NormalizeAndHydrateAsync(...) writes marker normalization during runtime.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs` lines 19-22: Schema initializer still requires scalar marker columns.

## P9-004 — Provider and resource plugin editors are still hardcoded by field key and hardcoded editor properties
- `src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor` lines 195-331: Resource editor renders fields via @switch(field.Key) across known keys only.
- `src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor` lines 271-295: Provider editor renders fields via @switch(field.Key) across three known keys only.
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs` lines 137-214: ResourceEditorModel is a hardcoded property bag for current plugins.
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` lines 113-143: ProviderProfileEditorModel is still a hardcoded current-plugin model.

## P9-005 — Custom plugins still persist bogus legacy enum identity
- `src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor.cs` lines 189-230: EnsureLegacyResourceKind / ResolveLegacyResourceKind still synthesize enum identity from plugin key.
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs` lines 363-370: SaveAsync persists entity.ResourceKind = connectorPlugin.LegacyResourceKind ?? model.ResourceKind.
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` lines 25-33: ProviderProfile still has active ProviderKind property.
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` lines 101-119: Provider summaries and editor model still expose ProviderKind as an active surface.
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` lines 313-316: SaveProviderAsync persists entity.ProviderKind = providerPlugin.LegacyProviderKind ?? model.ProviderKind.
- `src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs` lines 230-281: NewProvider(...) still defaults plugin identities through legacy ProviderKind presets.

## P9-006 — Node reference model is still closed-world and requires core edits for each new relation
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 8-22: ProjectNodeReferenceKind is still a fixed enum.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 59-67: ProjectNodeReferenceRecord.ReferenceId is still a Guid-only local identifier.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 108-148: ProjectNodeReferenceSet is still a fixed property bag for current relation kinds.

## P9-007 — Read-time normalization still performs writes in the hot load path
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs` lines 154-166: LoadAsync(...) still calls binding and marker NormalizeAndHydrateAsync(...) on reads.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` lines 158-238: NormalizeAndHydrateAsync(...) still persists changes via SaveChangesAsync.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeMarkerState.cs` lines 78-109: NormalizeAndHydrateAsync(...) still persists changes via SaveChangesAsync.

## P9-008 — Write-side connector boundary is not yet ready for upcoming external plugins
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchCrossModuleMutations.cs` lines 29-123: Current durable mutation model is scoped to internal project mutation records.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchCrossModuleMutationService.cs` lines 45-187: Current orchestration covers delete/move side effects, not a general connector command/outbox boundary.
