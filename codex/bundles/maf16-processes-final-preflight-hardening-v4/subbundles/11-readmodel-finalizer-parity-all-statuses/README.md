# SB11: 11-readmodel-finalizer-parity-all-statuses

## Goal

Make read model consume all artifact validation diagnostics.

## Required work

- Generalize `ResolveContentUnavailableArtifactDiagnostic` into validation diagnostic resolution for any finalizer status.
- If diagnostic says StaleOrWrongRun, read model must not say Satisfied.
- If diagnostic says ContentHashMismatch, read model must not say Satisfied.
- If diagnostic says WrongProducerMode/InvalidFormat/PlaceholderOnly, read model must expose it.
- Add tests for every status.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB11` are filled and the downstream dependency is safe.
