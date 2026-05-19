# Target Solution

- Extend probe contracts and persistence with strongly typed policy/projection context, then map API strings into existing value-object wrappers at the edge.
- Add a cognitive-memory database transfer handler registered through the module service collection. It copies explicit source-truth entities with manual field mapping and guards replacement when dependent memory data exists.
- Enrich status responses with small typed DTOs for database diagnostics, host diagnostics, and projection defaults. Use existing profile and options services; do not introspect connection strings in business logic beyond profile records already available.
- Refine dream aggregate construction with a primary-key-aware title resolver and canonical text that lists safe claim text plus source support summaries.
- Add cycle fields to scheduled automation request/result and loop consolidation through existing cursor semantics up to a bounded `MaxCycles`.
