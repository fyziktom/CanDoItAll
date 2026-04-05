# Symbol retirement gates

These are non-negotiable closure checks. Codex must not claim the finding is closed while these symbols/patterns remain in active architecture paths.

## P8-001 — Core node / binding boundary is still not sealed
- `builder.Property(item => item.Route)`
- `builder.Property(item => item.ExternalArtifactKind)`
- `builder.Property(item => item.MediaRelativePath)`
- `builder.Property(item => item.StorageObjectReferenceJson)`
- `ProjectObjectMetadataEnvelope foreign-owner Guid properties`

## P8-002 — Hierarchy is still dual represented and dual written
- `ProjectWorkbenchGraphConventions.ResolveHierarchyLinkKind usage in editable-node mutation paths`
- `ProjectWorkbenchGraphConventions.UpsertLinkAsync hierarchy writes in ProjectWorkbenchModels.cs / ProjectWorkbenchRelationService.cs / ProjectWorkbenchCrossModuleMutationService.cs`

## P8-003 — Node-kind registry is not yet the authoritative capability matrix
- `ResolveNodeAssignmentRoles`
- `ResolveParticipantRole`
- `RequiresCanonicalNode`
- `IsAllowedNodeType`

## P8-004 — Marker truth is still dual represented
- `MarkerIcon`
- `MarkerTone`
- `MarkerLabel`
- `ResolveMarkers(... legacyMarkerIcon, legacyMarkerTone, legacyMarkerLabel)`

## P8-005 — Plugin platform exists, but provider/resource domains and UIs are still legacy-enum driven
- `Enum.GetValues<ProviderKind>()`
- `Enum.GetValues<ResourceKind>()`
- `@switch (editor.ResourceKind)`
- `TryResolve(ProviderKind providerKind, string? connectorPluginKey, out IProviderAdapter adapter)`
- `ResolvePluginKey(ProviderKind providerKind)`

## P8-006 — External-side-effect integration boundary is still not durable enough for the next plugin wave
- `Direct side-effecting plugin calls inside request transaction paths`
- `Compensation-only closure as the sole reliability mechanism for future connector mutations`

## P8-007 — Major service and file hotspots are still too large
- `None (advisory decomposition finding)`
