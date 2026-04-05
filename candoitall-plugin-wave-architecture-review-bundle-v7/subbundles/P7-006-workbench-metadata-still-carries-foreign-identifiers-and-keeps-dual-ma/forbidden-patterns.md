# Forbidden patterns

The following patterns must be removed or made impossible:
- ParticipantIds in metadata
- MeetingNodeArtifactId / TranscriptNodeArtifactId / RecordingNodeArtifactId in metadata
- RepositoryResourceId / SecretReferenceArtifactId / StorageCatalogId in metadata
- ResolveMarkers fallback between metadata marker set and legacy marker columns

## Evidence anchors
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:219-247
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:287-331
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:388-477
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:545-585
