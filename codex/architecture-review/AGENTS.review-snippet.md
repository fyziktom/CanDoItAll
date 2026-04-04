# Architecture review snippet for AGENTS.md

Use this snippet in the repository `AGENTS.md` if you want Codex to stay aligned with the architecture-review workflow.

---

## Architecture review defaults

When asked to review architecture, canonical model integrity, source-of-truth boundaries, or post-feature stabilization:

1. Prefer the repo skills:
   - `$canonical-model-review`
   - `$feature-block-architecture-review`
   - `$architecture-drift-audit`

2. Treat **canonical model** questions as questions about:
   - what the real domain truth is
   - who owns each truth
   - which types are entities, relations, projections, events, policies, integration adapters, runtime state, or UI-only state

3. Never confuse:
   - canonical entities with projections
   - canonical truth with AI-generated proposals
   - domain state with UI state
   - live state with snapshot/import/export state
   - domain concepts with integration DTOs
   - project graph semantics with execution/runtime tooling semantics

4. For findings:
   - cite exact files, classes, methods, or symbols
   - mark assumptions explicitly
   - do not invent architecture intent without evidence
   - separate current facts from recommended future design

5. For C# solutions:
   - inspect the solution graph, project references, namespaces, models, services, repositories, and mapping code
   - prefer the smallest safe build/test validation needed to confirm a finding
   - avoid style-only criticism unless it hides a real architecture risk

6. For agent/AI features:
   - AI output is a proposal until validated and resolved into the canonical model
   - embeddings, semantic vectors, caches, summaries, and generated metadata are derived views unless the code explicitly models them as canonical truth

7. Always produce:
   - executive summary
   - findings by severity
   - stabilization plan split into **now / next wave / later**
