# Architecture Checkpoints

## Checkpoint table

| Checkpoint | Prerequisite subbundles | Gate | Downstream unlock |
|---|---|---|---|

## Checkpoint 1: Boundary foundation

Required proof:

- contracts extracted
- dependency direction valid
- no cyclic reference
- build passed

Decision:

## Checkpoint 2: Implementation extraction

Required proof:

- extracted type owns behavior
- old class delegates or no longer contains behavior
- direct unit tests passed
- negative test passed

Decision:

## Checkpoint 3: Construction and registration

Required proof:

- factory/builder/catalog wired
- composition smoke passed
- runtime does not directly construct concrete implementations

Decision:

## Checkpoint 4: Final architecture review

Required proof:

- `csharp-architecture-review-gate` passed
- old class shrink proof recorded
- unresolved bridges have follow-up subbundles

Decision:
