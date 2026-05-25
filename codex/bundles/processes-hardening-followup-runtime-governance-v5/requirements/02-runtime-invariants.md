# Runtime Invariants

## INV01

A step that does not allow `MutateProductTarget` must not mutate product files through direct tools, scripts, process-definition tools, workflow side effects, or subprocess projection.

## INV02

A step that only allows `WriteManagedProcessArtifacts` may write current-run process artifacts but must not create a product deliverable.

## INV03

A workflow-backed role may only satisfy process artifacts through explicit mapping or strongly validated lineage.

## INV04

A subprocess parent may only satisfy parent artifact expectations from child artifacts mapped by contract, not by loose kind/title matching alone.

## INV05

A missing upstream artifact block must be represented by typed state and be automatically reactivated when all missing inputs are materialized.

## INV06

Artifact validation must read actual stored bytes for file-backed evidence/deliverables whenever storage is available.

## INV07

A negative branch disposition must not mask failure to produce the current step's own required decision/artifact.

## INV08

Lint warnings that imply unsafe autonomous execution must become errors under strict/production/high-autonomy policy.
