# Assumptions And Risks

## Working Assumptions

- The active branch is `processes-hardening`.
- The canonical solution is `repo://CanDoItAll.slnx`.
- Workflow graph definitions remain the CanDoItAll canonical model and MAF is an execution adapter, not the persisted authoring model.
- Workspace file operations must remain scoped through existing workspace path policies.

## Critical Path Risks

- SB01 is a hard prerequisite because executor expansion is unsafe if planned or unavailable executors can be saved as runnable.
- SB02 is a hard prerequisite for executors that claim artifact output.
- SB03 is a hard prerequisite for local folder/file template and scenario coverage.
- SB08 can invalidate later UI/template proof if active helper nodes continue to pass through silently.

## Validation Risks

- A shallow test could verify catalog rows without proving save/import/publish/test validation uses the catalog.
- Artifact metadata tests could pass while no content is retrievable.
- File operation tests could pass only inside a permissive temp folder and miss workspace escape attempts.
- UI catalog tests can prove labels but not real authoring ergonomics; SB09 needs component proof and browser proof if layout changes.

## Reopen Triggers

- Reopen SB01 if any save/import/publish/test path accepts an unknown, planned, disabled, unavailable, or schema-invalid executor.
- Reopen SB02 if any payload policy artifact points to content that cannot be retrieved through the intended workspace or API boundary.
- Reopen SB03 if a file/folder executor operation bypasses workspace scoping, dry-run deletion, or explicit recursive confirmation.
- Reopen SB08 if any active helper node kind silently returns input without explicitly documented visual-only semantics.
- Reopen SB10 if final scenario proof relies on seeded records rather than production emitters and readers.

