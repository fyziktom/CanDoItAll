# Forbidden patterns

The following patterns must be removed or made impossible:
- ProjectObjectRecord.Route
- ProjectObjectRecord.ExternalArtifactKind / ExternalArtifactId
- ProjectObjectRecord.MediaRelativePath / MediaContentType / MediaOriginalFileName
- ProjectObjectRecord.StorageObjectReferenceJson

## Evidence anchors
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:26-59
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:143-177
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:165-244
