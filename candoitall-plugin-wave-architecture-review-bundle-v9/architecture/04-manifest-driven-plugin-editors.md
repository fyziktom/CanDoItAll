# Manifest-driven plugin editors
The shared editors should not know the field keys of each connector.

Recommended end-state:
- a generic connector state bag in the editor model,
- a renderer that chooses control type from `ConnectorConfigFieldType`,
- optional plugin-specific validation/adapters on top,
- save/load paths that round-trip unknown fields unchanged.

This is the difference between “current plugins happen to work” and “the platform is plugin-first.”
