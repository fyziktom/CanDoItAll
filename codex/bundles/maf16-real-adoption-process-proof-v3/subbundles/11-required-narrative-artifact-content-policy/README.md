# SB11: 11-required-narrative-artifact-content-policy

## Goal

Add explicit content policy for strict required narrative artifacts.

## Required work

- Define when Narrative/Decision artifacts must be content-backed.
- For strict process definitions, required Brief/Artifact contract records with managed path should require readable content unless marked manual/no-file.
- Add validation status and API display for RecordedButContentUnavailable or equivalent.
- Test first-step delivery contract with and without readable content.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB11` are updated and downstream subbundles can rely on it.
