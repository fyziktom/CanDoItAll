# Forbidden patterns

    Codex must not close this item while any of these remain active:

    - `builder.Property(item => item.Route)`
- `builder.Property(item => item.ExternalArtifactKind)`
- `builder.Property(item => item.MediaRelativePath)`
- `builder.Property(item => item.StorageObjectReferenceJson)`
- `ProjectObjectMetadataEnvelope foreign-owner Guid properties`
