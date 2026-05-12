# Normalized Requirements

| Requirement | Description | Owning subbundle | Planned proof |
| --- | --- | --- | --- |
| R1 | Default workflow examples must be stored as text files, not compiled graph-builder code. | 01, 02 | Source inspection and tests that load `Templates\Workflows`. |
| R2 | Template files must be YAML and file-backed, following the MAF declarative-loading pattern at the storage boundary. | 01 | Loader tests parse manifest and every YAML definition. |
| R3 | Loaded templates must map into strongly typed CanDoItAll workflow models before validation and persistence. | 01 | `WorkflowDefinitionValidator` passes for every loaded template. |
| R4 | Seeding must preserve managed refresh semantics, names, descriptions, component settings, graph behavior, and sample workspace assets. | 02 | Seed service tests and focused build. |
| R5 | YAML loading failures must be explicit and include path/key context; no silent fallback to compiled defaults. | 01, 02 | Negative loader test or explicit code review of exception path. |
| R6 | Future catalog/sharing work should be enabled by a stable folder and manifest layout, but not implemented in this bundle. | 01 | Architecture review and manifest shape. |
