## Strengths and fixed areas

- The biggest previously blocking issue — persisted synchronized projection truth — appears fixed. ProjectStructureAssemblyService now assembles external artifacts in memory, and integration tests explicitly assert that projection-only nodes/links/layout rows are not persisted.
- ProjectNodeBindingStorage and ProjectNodeReferenceRecord are good architectural moves. They show the codebase has already accepted the idea that bindings/references need their own ownership boundary.
- ProjectWorkbenchLifecycleService plus ProjectNodeLifecycleEventRecord give note→typed-node evolution real historical evidence. Reclassification is no longer an untracked mutation.
- ProjectNodeKindRegistry is a meaningful step toward canonical node semantics. Visual profile, normalization, subtype mutation, and note promotion are no longer scattered everywhere.
- Connector manifests and plugin registries now exist, especially on the resource side. This is a strong foundation for the future plugin platform even though the active UI/domain flow is not fully plugin-first yet.
- Architecture tests and integration tests are better than before. There is real evidence that some prior bundle recommendations were implemented.
