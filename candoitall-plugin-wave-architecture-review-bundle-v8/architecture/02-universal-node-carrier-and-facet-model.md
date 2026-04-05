## Universal node carrier and facet model

### Keep on node core
- project id / node key / type / subtype
- title / subtitle / notes / status
- parent relationship (or dedicated hierarchy owner if you choose that route)
- XY
- canonical marker set
- schedule / duration / progress
- lifecycle continuity and created/updated timestamps

### Move out of node core
- routes
- external artifact identity
- uploaded media descriptors
- storage object references
- foreign-owner IDs (provider profile, participant artifact, resource artifact, storage catalog, secret artifact, etc.)
- plugin-specific configuration
- write-side connector operation state

### Why this fits the product
This preserves the “node as universal carrier” idea while still drawing a clean boundary between:
- the thing the user is modeling on the mindmap,
- the bindings/facets that enrich that node,
- and the projections that expose it in different modules.
