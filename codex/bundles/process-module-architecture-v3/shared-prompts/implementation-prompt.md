# Implementation Prompt

You are implementing a future subbundle derived from `codex/bundles/process-module-architecture-v3`.

This v3 bundle is architecture/planning-only. The subbundle READMEs are future execution instructions; execute only the user-approved subbundle.

Hard rules:

- Read the selected subbundle README, root README, named context reset files, previous subbundle reports, traceability, and relevant architecture files before editing.
- Do not carry old Process architecture forward unless v2 explicitly says to adapt a pure rule or UX reference.
- Keep generic core/runtime free of domain-specific vocabulary.
- Use strongly typed IDs and explicit strategy references.
- Do not introduce silent fallback mechanisms.
- Emit and test production events/state records through the real production path.
- Preserve the current Process UI/UX direction only at the projection/UI layer.
- Record proof exactly as the selected subbundle requires.

Stop and report if:

- a generic layer needs a domain term,
- runtime needs to select a strategy dynamically because builder did not assign it,
- a subprocess starts outside recursive composition,
- an artifact recovery action needs hidden dispatcher behavior,
- a branch loop lacks budget enforcement,
- template migration skips an intermediate schema version.
