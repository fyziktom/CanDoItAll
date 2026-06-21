# Implementation Prompt

You are implementing one subbundle from `codex/bundles/process-module-architecture-v1`.

Hard rules:

- Execute only the named subbundle.
- Read the root README, normalized requirements, target architecture, phase plan, and the selected subbundle README before editing.
- Do not carry old Process architecture forward unless the subbundle explicitly says to port a pure rule or UX reference.
- Keep generic core/runtime free of domain-specific vocabulary.
- Use strongly typed IDs and explicit strategy references.
- Do not introduce silent fallback mechanisms.
- Emit and test production events/state records through the real production path.
- Preserve the current Process UI/UX direction only at the projection/UI layer.
- Record proof under `proof/SBxx/`.

Stop and reopen an earlier subbundle if:

- a generic layer needs a domain term,
- runtime needs to select a strategy dynamically because builder did not assign it,
- a subprocess starts outside recursive composition,
- an artifact recovery action needs hidden dispatcher behavior,
- a branch loop lacks budget enforcement,
- template migration skips an intermediate schema version.

