# Current State Review

The phase5 branch made real progress:

- `ProcessStepOperation`, `ProcessStepTargetScope`, and persisted allowed operations now exist.
- Process dispatch emits allowed operations, target scope, and product mutation metadata.
- Tool policy now evaluates operation requirements for validation, runtime launch, browser proof, file mutation, process artifact recording, step transition, and process-definition mutation tools.
- Script policy inspection now exists and inspects script content before execution.
- Artifact validation now has a `WorkspaceProcessArtifactContentReader` and validates stored content for JSON, Markdown, YAML, and image artifacts.
- Projection lineage is typed and serialized in `ProjectionLineageJson`.
- `ProcessStepRun` now has typed block reason and recovery option fields.
- Missing upstream artifact materialization now re-checks the newly created in-memory artifact before `SaveChanges`.

However, several issues remain because the new runtime is a hybrid of typed contracts and legacy inference.
