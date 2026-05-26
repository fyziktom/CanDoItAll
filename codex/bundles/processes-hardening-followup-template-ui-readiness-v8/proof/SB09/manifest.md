# SB09 Proof Manifest

## Status

Completed.

## Production-path coverage

- Strict process-definition lint now errors when workflow-backed required artifacts omit explicit workflow output mapping fields.
- Strict process-definition lint now errors when subprocess parent required artifacts omit explicit child artifact expectation mappings.
- Template artifact projection preserves `WorkflowOutputId`, `WorkflowOutputName`, `WorkflowOutputKind`, and `SubprocessChildArtifactExpectationId`.
- The artifact expectation editor exposes the mapping fields already persisted by the definition model.

## Semantic invariant

See `proof/SB09/semantic-invariants.md`.

## Failing-first or adversarial proof

`proof/SB09/transcripts/failing-first.txt`

## Passing proof

`proof/SB09/transcripts/passing.txt`

## Source assertions

`proof/SB09/transcripts/source-assertions.txt`

## Anti-stub audit

`proof/SB09/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`proof/SB09/transcripts/changed-file-hashes.txt`
