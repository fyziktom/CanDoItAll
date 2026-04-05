# Symbol retirement gates
The following active symbols/patterns must disappear or become non-active compatibility-only migration code outside hot paths:

## Legacy carrier
- `src/CanDoItAll.Modules.Workbench/ProjectObjectRecord.LegacyCarrier.cs`
- `ProjectObjectRecord.Route`
- `ProjectObjectRecord.ExternalArtifactKind`
- `ProjectObjectRecord.ExternalArtifactId`
- `ProjectObjectRecord.MediaRelativePath`
- `ProjectObjectRecord.MediaContentType`
- `ProjectObjectRecord.MediaOriginalFileName`
- `ProjectObjectRecord.StorageObjectReferenceJson`

## Marker dual truth
- `ProjectObjectRecord.MarkerIcon`
- `ProjectObjectRecord.MarkerTone`
- `ProjectObjectRecord.MarkerLabel`
- `ProjectNodeMarkerState.ResolveLegacyJson`
- `ProjectNodeMarkerState.HydrateLegacyFields`

## Plugin-first closure
- `@switch (field.Key)` in shared provider/resource editors
- `EnsureLegacyResourceKind`
- `ResolveLegacyResourceKind`
- `entity.ResourceKind = connectorPlugin.LegacyResourceKind ?? model.ResourceKind`
- `entity.ProviderKind = providerPlugin.LegacyProviderKind ?? model.ProviderKind`

## Closed-world node references
- `enum ProjectNodeReferenceKind`
- `class ProjectNodeReferenceSet`
- `ProjectNodeReferenceRecord.ReferenceId : Guid`
