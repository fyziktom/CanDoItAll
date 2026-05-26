# SB06 Semantic Invariants

- Invariant ID: SB06-INV-001
- Source raw note: RQ02 typed template operation contracts and RQ03 Blazor boundary correctness.
- Expected behavior: Declared process-step operation contracts are normalized through one authoritative service that adds target-scope implied operations, preserves missing-contract visibility, validates contradictory operation/scope combinations, and is consumed by save/read, import/export, template projection, lint, runtime read, and dispatch metadata paths.
- Disallowed shallow implementation: docs-only, prompt-only, fixture-only, or test-only changes that do not exercise production code paths.
- Failing-first test: `bundle://proof/SB06/transcripts/failing-first.txt` shows strict lint previously missed invalid operation/scope combinations.
- Passing test: `bundle://proof/SB06/transcripts/passing.txt` covers direct normalization, strict lint, API round-trip, template projection, and dispatcher persisted-contract resolution.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessStepOperationContractState.cs` plus save/import/template/runtime/lint/dispatch callers listed in `bundle://proof/SB06/transcripts/changed-file-hashes.txt`.
- Production assertions: one shared normalizer owns implied operations, target-scope inference, step-kind defaults, and invalid-combination validation.
- Red-team negative case: contradictory persisted contracts are rejected rather than normalized differently by API, template, UI, or dispatcher paths.
- Downstream dependency check: SB07 and SB08 can rely on a single contract normalization boundary before project-structure policy and non-software template migration.
- Required proof: failing-first/adversarial proof, passing production-path integration tests, source assertions, anti-stub audit, changed-file hashes.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| Operation contract normalization state | `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessStepOperationContractState.cs` | `repo://src/CanDoItAll.Modules.Processes/Persistence/ProcessesService.Persistence.DefinitionChildren.Steps.cs` and runtime/template callers | `bundle://proof/SB06/transcripts/source-assertions.txt` shows save, import, lint, read, and dispatch call sites | Invalid operation/scope combinations fail strict lint instead of being accepted by a different path |
