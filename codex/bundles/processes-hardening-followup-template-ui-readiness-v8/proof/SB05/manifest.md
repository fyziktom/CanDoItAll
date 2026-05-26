# SB05 Proof Manifest

## Status

 Completed.

## Semantic invariant

See `proof/SB05/semantic-invariants.md`.

## Failing-first or adversarial proof

`proof/SB05/transcripts/failing-first.txt`

## Passing proof

`proof/SB05/transcripts/passing.txt`

## Production-path coverage

- `repo://Templates/Processes/seed-catalog/baseline-scenarios.json` contains the reusable `baseline-blazor-wasm-pwa-tetris` scenario for the generic `blazor-app-delivery` process.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs` verifies the scenario through the production template catalog/projection path and reasserts the Blazor step mutation boundaries used by the scenario.

## Source assertions

`proof/SB05/transcripts/source-assertions.txt`

## Anti-stub audit

`proof/SB05/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`proof/SB05/transcripts/changed-file-hashes.txt`
