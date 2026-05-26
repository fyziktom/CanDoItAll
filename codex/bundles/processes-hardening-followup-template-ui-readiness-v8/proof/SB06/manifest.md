# SB06 Proof Manifest

## Status

 Completed.

## Semantic invariant

See `proof/SB06/semantic-invariants.md`.

## Failing-first or adversarial proof

`proof/SB06/transcripts/failing-first.txt`

## Passing proof

`proof/SB06/transcripts/passing.txt`

## Production-path coverage

- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessStepOperationContractState.cs` is now the authoritative normalizer for implied operations, target-scope inference, default operation sets, and invalid-combination validation.
- Editor save/read, import/export, template projection, runtime read models, lint, and dispatch metadata use the same declared-contract normalization surface.
- Focused integration coverage exercises direct normalization, strict lint rejection, API round-trip normalization, template projection, and dispatcher persisted-contract resolution.

## Source assertions

`proof/SB06/transcripts/source-assertions.txt`

## Anti-stub audit

`proof/SB06/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`proof/SB06/transcripts/changed-file-hashes.txt`

- `5A379C1CBF07E9D72B5359063B6D54EA7E005FA8F885D7A5EA58A2FCE7EB6D2C` `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessStepOperationContractState.cs`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| Operation contract normalization state | `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessStepOperationContractState.cs` | `repo://src/CanDoItAll.Modules.Processes/Persistence/ProcessesService.Persistence.DefinitionChildren.Steps.cs` and template/runtime callers | Created during save/import/template/runtime normalization and reused by lint and dispatch metadata | Invalid operation/scope combinations fail strict lint instead of persisting contradictory contracts |
