# Implementation Prompt

You are preparing or implementing a future bundle derived from `codex/bundles/process-module-architecture-v2`.

This v2 bundle is architecture-only. Do not execute stale v1 subbundle files and do not treat the deferred marker in `subbundles/` as an implementation package.

Hard rules:

- Create a fresh implementation bundle for the selected phase before editing product source.
- Read the root README, normalized requirements, traceability, target architecture files, Phase 0 plan, and project-by-project rebuild plan before editing.
- Do not carry old Process architecture forward unless v2 explicitly says to adapt a pure rule or UX reference.
- Keep generic core/runtime free of domain-specific vocabulary.
- Use strongly typed IDs and explicit strategy references.
- Do not introduce silent fallback mechanisms.
- Emit and test production events/state records through the real production path.
- Preserve the current Process UI/UX direction only at the projection/UI layer.
- Record proof under the future implementation bundle proof directory.

Stop and reopen architecture if:

- a generic layer needs a domain term,
- runtime needs to select a strategy dynamically because builder did not assign it,
- a subprocess starts outside recursive composition,
- an artifact recovery action needs hidden dispatcher behavior,
- a branch loop lacks budget enforcement,
- template migration skips an intermediate schema version.
