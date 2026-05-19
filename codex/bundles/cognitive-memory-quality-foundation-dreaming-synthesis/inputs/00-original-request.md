# Original Request

The user asked for a senior C# architecture and neuroscience review of the current Cognitive Memory implementation. The docs are considered updated, but older multi-step validation bundles were created before P0/P1 refactors and are not fully current. The user wants a detailed execution-grade bundle with subbundles for Codex.

Required focus areas:

- Review the current implementation in detail.
- Identify major missing pieces and quality risks.
- Focus on clustering by different keys.
- Focus on the dreaming mode, where memory should organize memories, create aggregations, and validate them.
- Investigate why dreaming/consolidation appears suspiciously fast and whether it is doing enough work.
- Focus on use of memories: the system should not simply retrieve and pass thoughts forward; it should formulate and combine useful information for the consumer without flooding them with scores and references.
- Preserve the ability to answer follow-up reference questions by showing which specific source memories/references produced a synthesized statement.
- Do not include economic models or memory governance economics yet.
