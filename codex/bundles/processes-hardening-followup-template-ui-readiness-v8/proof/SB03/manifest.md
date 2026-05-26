# SB03 Proof Manifest

## Status

Completed.

## Semantic invariant

SB03-INV-001: every manifest process template has step-level governance inventory covering typed operation fields, branch outcomes, required artifacts, artifact inputs, exception policy, readiness, and a concrete migration owner when typed fields are still missing.

See `bundle://proof/SB03/semantic-invariants.md`.

## Failing-first or adversarial proof

`bundle://proof/SB03/transcripts/failing-first.txt`

The strict typed-contract audit failed before migration work because 104 of 147 manifest steps lacked persisted typed operation contracts; every gap had an explicit downstream migration owner.

## Passing proof

`bundle://proof/SB03/transcripts/passing.txt`

The matrix-generation audit passed with 21 templates, 147 steps, 104 typed-contract gaps, and 0 missing migration plans. Matrix: `bundle://proof/SB03/template-governance-matrix.md`.

## Source assertions

`bundle://proof/SB03/transcripts/source-assertions.txt`

## Anti-stub audit

`bundle://proof/SB03/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`bundle://proof/SB03/transcripts/changed-file-hashes.txt`

- `9662C83DD12DD84A58BBBD2A79400D0B93E68E1BA0A6B776B92A2E04FA63249F` `repo://codex/bundles/processes-hardening-followup-template-ui-readiness-v8/scripts/audit-template-governance.ps1`
- `4F8E616998A9E2A723BA48E7D7BB3ED21D82CAFA04BE340B01E9724F4BED1CE2` `repo://codex/bundles/processes-hardening-followup-template-ui-readiness-v8/proof/SB03/template-governance-matrix.md`
- `4DD169CA7B7765DF21AD527ECBB967C62BE9ADA12E658953686E087239C2B340` `repo://Templates/Processes/manifest.json`
