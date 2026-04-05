## Target solution

The target architecture should stabilize around these principles:

1. **Node core stays real and central.**  
   It owns identity, canonical structure, XY, markers, schedule, title/subtitle/notes, status/progress, subtype identity, and lifecycle continuity.

2. **Bindings/facets/references sit beside node core, not inside it.**  
   Media, routes, artifact links, provider/resource references, and other foreign-owner identifiers should live behind explicit records/services.

3. **Assembly builds projections, not truth.**  
   External artifacts and system-managed surfaces should be assembled in memory from canonical sources plus layout overrides.

4. **Registry owns semantics.**  
   Node families, reclassification rules, metadata normalization, assignment roles, node-scoped capabilities, and allowed reference kinds must come from one authoritative semantic registry/capability layer.

5. **Plugins are manifest-first.**  
   Provider/resource/connector flows resolve by plugin key + manifest + schema, not by enums and switch pages.

6. **Write-side integrations are durable.**  
   External side effects execute through durable intents/outbox/background operations rather than inline mutation + compensation alone.
