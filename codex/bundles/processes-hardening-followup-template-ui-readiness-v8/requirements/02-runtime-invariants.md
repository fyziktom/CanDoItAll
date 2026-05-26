# Runtime Invariants

1. A process step cannot complete with required artifacts unless artifact validation passes through a shared finalizer-grade validator.
2. A review/validation step cannot mutate product files unless its explicit operation contract allows product mutation.
3. A writeback step can write managed process artifacts and controlled project-structure records, but must not mutate product source files.
4. A process API/tool path cannot omit typed operation contract fields, projection lineage, block cause, or recovery state.
5. A template import/export round trip must preserve operation contract, artifact mappings, contract mode, and workflow/subprocess output mapping fields.
6. A process template cannot rely on prose-only operation boundaries once typed fields are supported.
7. Project-structure mutation tools must be classified and governed by `ExecuteExternalAction`.
8. Tetris/Blazor specifics must live in template/test data and instructions, not in the process runtime core.
