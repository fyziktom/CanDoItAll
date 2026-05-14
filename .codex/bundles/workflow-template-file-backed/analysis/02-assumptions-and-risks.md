# Assumptions And Risks

## Working Assumptions

- The default workflow examples currently compiled in `WorkflowExampleCatalogSeedService` are the source of truth for this migration.
- A `Templates\Workflows` pack is more maintainable than embedding YAML as resources because users and future catalog tooling can inspect and edit plain files.
- The loader should convert YAML DTOs into existing `WorkflowGraph`, `WorkflowNode`, `WorkflowEdge`, and component save requests before validation.

## Critical Path Risks

- If the YAML schema is too close to C# construction helpers, the catalog will still be hard to author and share.
- If the YAML schema is too generic, the loader becomes stringly typed and hides errors until runtime.
- If component placeholders are handled loosely, seeded workflows can point at stale component ids after refresh.

## Validation Risks

- Loading YAML without running `WorkflowDefinitionValidator` would prove parsing, not correctness.
- Tests must cover all default templates, not just a sample, because one malformed template can block startup seeding.
- Build validation must include the module project that owns seeding and the unit test project that owns workflow catalog tests.

## Reopen Triggers

- Reopen subbundle 01 if any template cannot round-trip into a valid `WorkflowGraph` without special-case compiled graph code.
- Reopen subbundle 02 if seeding still builds default graphs or routing instructions in code.
- Reopen subbundle 03 if tests only assert file existence and do not validate loaded definitions.
