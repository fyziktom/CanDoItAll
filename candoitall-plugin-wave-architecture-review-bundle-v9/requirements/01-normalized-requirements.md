# Normalized requirements
R1. Keep `ProjectObjectRecord` as the universal project/canvas carrier, but only for canonical node data.
R2. Binding / route / media / external-artifact transport state must live outside the persisted node carrier.
R3. Marker truth must have exactly one canonical persisted representation.
R4. Plugin editors must render manifest-defined fields generically by declared field type.
R5. Connector plugin key must be the authoritative connector identity for new/custom plugins.
R6. Node references must support plugin-defined relation kinds without core enum/property expansion.
R7. Read paths must be read-only after the migration/repair phase completes.
R8. Before write-side external connectors ship, a generic durable connector command boundary must exist.
