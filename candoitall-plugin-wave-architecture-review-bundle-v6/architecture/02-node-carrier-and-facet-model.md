# Node Carrier and Facet Model

## Suggested core persistence shape

### Carrier

A canonical carrier row keyed by node id / node key should own:

- project id
- node key
- active kind key
- parent node key
- title / subtitle / notes
- semantic X/Y
- semantic marker set or marker fields
- schedule anchors
- minimal status / priority when truly cross-kind
- origin / authorship / timestamps

### Facets

Facet rows should model kind-family-specific payload, for example:

- work-item facet
- decision facet
- meeting facet
- participant facet
- repository facet
- transcript facet
- connector facet

A node has at most one active facet instance for its active family, but history may keep superseded facet snapshots.

### Bindings

Bindings should hold foreign canonical references, for example:

- artifact binding
- resource binding
- provider profile binding
- connector account binding
- storage object binding
- secret binding

Bindings are not the same as semantic relations.

## Hierarchy vs semantic relations

Hierarchy should be canonical once.

- canonical hierarchy: `ParentNodeKey` (or a dedicated canonical tree table if that is preferred)
- semantic relation table: `DependsOn`, `Blocks`, `Uses`, `Validates`, `Tests`, `RelatedTo`, and similar

`Contains` / `BelongsTo` should not remain as a second canonical containment truth.

## Projection materialization rule

If a foreign-module item should become a real project-owned reasoning object, do it explicitly:

- assemble it as a read-only projection first
- materialize it into a canonical node only when the user intentionally adopts it into project planning

That preserves the universal node model without forcing every foreign module row into canonical node storage.
