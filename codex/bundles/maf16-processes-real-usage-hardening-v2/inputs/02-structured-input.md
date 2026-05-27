# Structured Input

## Objectives

- Prove MAF 1.6 package adoption through package, build, and runtime tests.
- Decide and document adopted, deferred, blocked, and not-applicable MAF 1.6 features.
- Apply useful MAF 1.6 feature adoption without leaking MAF internals into process domain models.
- Harden process artifact validation, storage, lineage, read-model parity, recovery, and operator approval semantics.
- Validate web-app startup, browser visibility, and simple agent communication after the changes.

## Constraints

- Keep changes small and strongly typed.
- Preserve existing Blazor/Radzen/component-library patterns.
- Do not hardcode the Blazor/Tetris live-run case into generic process runtime code.
- Do not weaken validation statuses or convert unreadable content into satisfied artifacts.
- Do not rely on mock-only proof where the bundle requires integration-path or live-run proof.

## High-Risk Inputs

- MAF adapter still resembles pre-upgrade code.
- Finalizer and tool-loop instructions may be prompt-concatenated instead of injected.
- Artifact records may be deduplicated by display/external keys in ways that hide wrong lineage.
- Operator decisions may be mistaken for deliverable evidence if producer mode and lineage are loose.
